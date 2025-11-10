import * as React from 'react';
import { WebPartContext } from '@microsoft/sp-webpart-base';
import { SPHttpClient, SPHttpClientResponse, SPHttpClientConfiguration } from '@microsoft/sp-http';
import {
  DocumentCard,
  DocumentCardTitle,
  ProgressIndicator,
  MessageBar,
  MessageBarType,
  PrimaryButton,
  Stack,
  Text
} from '@fluentui/react';
import { FunctionAppService, ProcessSharePointFileRequest } from '../../../services/FunctionAppService';

export interface IDocumentUploaderProps {
  context: WebPartContext;
  libraryName: string;
  functionAppUrl: string;
  httpClient: SPHttpClient;
}

export interface IDocumentUploaderState {
  uploadStatus: 'idle' | 'uploading' | 'success' | 'error';
  message: string;
  enrichedDocuments: any[];
}

export default class DocumentUploader extends React.Component<IDocumentUploaderProps, IDocumentUploaderState> {
  private functionAppService: FunctionAppService;
  private _fileInput: HTMLInputElement | null = null;

  constructor(props: IDocumentUploaderProps) {
    super(props);
    this.state = {
      uploadStatus: 'idle',
      message: '',
      enrichedDocuments: []
    };
    this.functionAppService = new FunctionAppService(props.functionAppUrl);
  }

  private async ensureLibraryExists(libraryName: string): Promise<void> {
    const webUrl = this.props.context.pageContext.web.absoluteUrl;
    
    // Check if library exists
    const checkUrl = `${webUrl}/_api/web/lists/getbytitle('${encodeURIComponent(libraryName)}')`;
    try {
      const checkResponse = await this.props.httpClient.get(checkUrl, SPHttpClient.configurations.v1);
      if (checkResponse.ok) {
        console.log(`✅ Library '${libraryName}' already exists`);
        return;
      }
    } catch (error: any) {
      // Library doesn't exist (404 or other error), will create it
      console.log(`⚠️ Library '${libraryName}' not found. Attempting to create document library...`);
      console.log(`   Note: If you created a FOLDER with this name, please create a DOCUMENT LIBRARY instead.`);
    }

    // Create document library
    // Use the correct endpoint with entity set specified
    const createUrl = `${webUrl}/_api/web/lists`;
    
    // Use odata=nometadata to avoid JSON Light parsing issues
    const createBody = JSON.stringify({
      '__metadata': { 'type': 'SP.List' },
      'BaseTemplate': 101, // Document Library template
      'Title': libraryName,
      'Description': 'SMEPilot document library for uploaded files'
    });

    try {
      // Get request digest (required for POST)
      const digest = await this.getRequestDigest();
      
      if (!digest) {
        throw new Error('Could not obtain request digest. Please refresh the page and try again.');
      }
      
      const headers: any = {
        'Accept': 'application/json;odata=verbose',
        'Content-Type': 'application/json;odata=verbose',
        'X-RequestDigest': digest
      };

      console.log('Creating library:', libraryName);
      const createResponse = await this.props.httpClient.post(createUrl, SPHttpClient.configurations.v1, {
        body: createBody,
        headers: headers
      });

      if (!createResponse.ok) {
        const errorText = await createResponse.text().catch(() => 'Unknown error');
        console.error('Failed to create library:', createResponse.status, errorText);
        
        // Parse error if possible
        let errorMessage = `Failed to create library (${createResponse.status})`;
        try {
          const errorJson = JSON.parse(errorText);
          if (errorJson.error?.message) {
            errorMessage = errorJson.error.message;
          }
        } catch (e) {
          // Not JSON, use text as-is
        }
        
        throw new Error(errorMessage);
      }

      const responseData = await createResponse.json().catch(() => ({}));
      console.log(`Library '${libraryName}' created successfully`, responseData);
      
      // Wait a moment for SharePoint to process the creation
      await new Promise(resolve => setTimeout(resolve, 1500));
    } catch (error: any) {
      console.error('Error creating library:', error);
      const errorMsg = error?.message || String(error);
      
      // If it's a permissions error, provide helpful message
      if (errorMsg.includes('403') || errorMsg.includes('Unauthorized') || errorMsg.includes('permission')) {
        throw new Error(`Cannot create library '${libraryName}'. You need "Manage Lists" permission. Please create the library manually or contact your SharePoint admin.`);
      }
      
      // Otherwise, throw the original error
      throw new Error(`Cannot create library '${libraryName}'. Error: ${errorMsg}`);
    }
  }

  private async getRequestDigest(): Promise<string> {
    try {
      const webUrl = this.props.context.pageContext.web.absoluteUrl;
      const digestUrl = `${webUrl}/_api/contextinfo`;
      const response = await this.props.httpClient.post(digestUrl, SPHttpClient.configurations.v1, {
        body: ''
      });
      
      if (!response.ok) {
        console.warn('Failed to get request digest:', response.status);
        return '';
      }
      
      const data = await response.json();
      // Handle both odata=verbose and odata=nometadata formats
      const digest = data.d?.GetContextWebInformation?.FormDigestValue || 
                     data.FormDigestValue || 
                     '';
      
      if (digest) {
        console.log('Request digest obtained');
      } else {
        console.warn('Request digest is empty');
      }
      
      return digest;
    } catch (error) {
      console.warn('Could not get request digest:', error);
      return '';
    }
  }

  private async uploadFile(file: File): Promise<void> {
    this.setState({ uploadStatus: 'uploading', message: 'Preparing upload...' });

    try {
      // 0. Ensure library exists
      await this.ensureLibraryExists(this.props.libraryName);
      
      this.setState({ message: 'Uploading document to SharePoint...' });

      // 1. Upload to SharePoint ScratchDocs library
      const libraryUrl = `${this.props.context.pageContext.web.absoluteUrl}/_api/web/lists/getbytitle('${this.props.libraryName}')/RootFolder`;
      const fileBuffer = await file.arrayBuffer();
      
      // Get request digest for file upload
      const digest = await this.getRequestDigest();
      if (!digest) {
        throw new Error('Could not obtain request digest for file upload');
      }
      
      const uploadUrl = `${libraryUrl}/Files/Add(url='${encodeURIComponent(file.name)}', overwrite=true)`;
      
      // For SharePoint file uploads, we need to use the correct configuration
      // SPHttpClient.configurations.v1 automatically sets Accept header correctly
      // We only need to add X-RequestDigest for POST requests
      const uploadResponse = await this.props.httpClient.post(uploadUrl, SPHttpClient.configurations.v1, {
        body: fileBuffer,
        headers: {
          'X-RequestDigest': digest
          // Let SPHttpClient handle Accept and Content-Type headers automatically
        }
      });

      if (!uploadResponse.ok) {
        const errorText = await uploadResponse.text().catch(() => 'Unknown error');
        throw new Error(`File upload failed (${uploadResponse.status}): ${errorText}`);
      }

      this.setState({ message: 'File uploaded. Triggering template formatting...' });

      // 2. Trigger template formatting via Function App
      const tenantId = this.props.context.pageContext.aadInfo?.tenantId?.toString() || 'default';
      
      const request: ProcessSharePointFileRequest = {
        siteId: this.props.context.pageContext.site.id.toString(),
        driveId: this.props.context.pageContext.web.id.toString(),
        itemId: 'temp', // Function App will resolve from file name
        fileName: file.name,
        uploaderEmail: this.props.context.pageContext.user.email || '',
        tenantId: tenantId
      };

      const response = await this.functionAppService.processSharePointFile(request);

      this.setState({ 
        uploadStatus: 'success', 
        message: `Document "${file.name}" uploaded and template formatting started! Formatted document: ${response.enrichedUrl}` 
      });

      // Refresh formatted documents list
      setTimeout(() => this.loadEnrichedDocuments(), 2000);
    } catch (error: any) {
      this.setState({ 
        uploadStatus: 'error', 
        message: `Upload failed: ${error.message}` 
      });
    }
  }

  private async loadEnrichedDocuments(): Promise<void> {
    try {
      const webUrl = this.props?.context?.pageContext?.web?.absoluteUrl;
      if (!webUrl) {
        console.warn('Missing webUrl in props.context.pageContext.web.absoluteUrl');
        this.setState({ enrichedDocuments: [], uploadStatus: 'idle' });
        return;
      }

      // libraryName property fallback
      const library = encodeURIComponent(this.props.libraryName || 'SMEPilot-Enriched');
      const processedDocsUrl = `${webUrl}/_api/web/lists/getbytitle('${library}')/items?$select=Id,Title,FileRef,Created&$top=10&$orderby=Created desc`;

      console.log('Fetching enriched docs from', processedDocsUrl);
      const response: SPHttpClientResponse = await this.props.httpClient.get(processedDocsUrl, SPHttpClient.configurations.v1);
      
      if (!response.ok) {
        // If list doesn't exist (404), that's OK - it will be created on first upload
        if (response.status === 404) {
          console.log('Library not found - will be created on first upload');
          this.setState({ enrichedDocuments: [], uploadStatus: 'idle', message: '' });
          return;
        }
        const text = await response.text().catch(() => '<no body>');
        console.error('Failed to fetch enriched docs', response.status, text);
        this.setState({ enrichedDocuments: [], uploadStatus: 'idle', message: 'Unable to load enriched docs' });
        return;
      }

      const data = await response.json();
      this.setState({ enrichedDocuments: data.value || [], uploadStatus: 'idle', message: '' });
    } catch (err) {
      console.error('Error loading enriched documents', err);
      this.setState({ enrichedDocuments: [], uploadStatus: 'error', message: String(err) });
    }
  }

  public componentDidMount(): void {
    console.log('DocumentUploader mounted', { props: this.props });
    // defensive load
    this.loadEnrichedDocuments().catch(err => {
      console.error('loadEnrichedDocuments failed', err);
      this.setState({ 
        enrichedDocuments: [], 
        uploadStatus: 'error', 
        message: 'Failed to load enriched documents: ' + (err?.message || err) 
      });
    });
  }

  public render(): React.ReactElement<IDocumentUploaderProps> {
    return (
      <Stack tokens={{ childrenGap: 15 }} style={{ padding: '20px' }}>
        <Text variant="xLarge">SMEPilot - Document Template Formatting</Text>
        
        {this.state.message && (
          <MessageBar messageBarType={
            this.state.uploadStatus === 'error' ? MessageBarType.error :
            this.state.uploadStatus === 'success' ? MessageBarType.success :
            MessageBarType.info
          }>
            {this.state.message}
          </MessageBar>
        )}
        
        {this.state.uploadStatus === 'uploading' && (
          <ProgressIndicator label="Uploading and formatting document..." />
        )}

        <Stack horizontal tokens={{ childrenGap: 10 }}>
          <input
            type="file"
            accept=".docx"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) {
                console.log('File selected:', file.name);
                this.uploadFile(file);
              }
            }}
            style={{ display: 'none' }}
            id="file-upload-input"
            ref={(input) => { this._fileInput = input; }}
          />
          <PrimaryButton 
            text="Upload Document (.docx)" 
            onClick={() => {
              console.log('Upload button clicked');
              if (this._fileInput) {
                this._fileInput.click();
              }
            }}
          />
        </Stack>

        {this.state.enrichedDocuments.length > 0 && (
          <Stack tokens={{ childrenGap: 10 }}>
            <Text variant="mediumPlus">Recently Formatted Documents</Text>
            {this.state.enrichedDocuments.map((doc: any) => (
              <DocumentCard key={doc.Id}>
                <DocumentCardTitle title={doc.Title || doc.FileRef} />
              </DocumentCard>
            ))}
          </Stack>
        )}
      </Stack>
    );
  }
}

