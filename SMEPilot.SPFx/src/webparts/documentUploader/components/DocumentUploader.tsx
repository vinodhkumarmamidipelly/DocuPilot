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

  constructor(props: IDocumentUploaderProps) {
    super(props);
    this.state = {
      uploadStatus: 'idle',
      message: '',
      enrichedDocuments: []
    };
    this.functionAppService = new FunctionAppService(props.functionAppUrl);
  }

  private async uploadFile(file: File): Promise<void> {
    this.setState({ uploadStatus: 'uploading', message: 'Uploading document to SharePoint...' });

    try {
      // 1. Upload to SharePoint ScratchDocs library
      const libraryUrl = `${this.props.context.pageContext.web.absoluteUrl}/_api/web/lists/getbytitle('${this.props.libraryName}')/RootFolder`;
      const fileBuffer = await file.arrayBuffer();
      
      const uploadUrl = `${libraryUrl}/Files/Add(url='${encodeURIComponent(file.name)}', overwrite=true)`;
      
      await this.props.httpClient.post(uploadUrl, SPHttpClient.configurations.v1, {
        body: fileBuffer
      });

      this.setState({ message: 'File uploaded. Triggering enrichment...' });

      // 2. Trigger enrichment via Function App
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
        message: `Document "${file.name}" uploaded and enrichment started! Enriched document: ${response.enrichedUrl}` 
      });

      // Refresh enriched documents list
      setTimeout(() => this.loadEnrichedDocuments(), 2000);
    } catch (error: any) {
      this.setState({ 
        uploadStatus: 'error', 
        message: `Upload failed: ${error.message}` 
      });
    }
  }

  private async loadEnrichedDocuments(): Promise<void> {
    // Load enriched documents from ProcessedDocs library
    // Implementation to fetch from SharePoint
    try {
      const processedDocsUrl = `${this.props.context.pageContext.web.absoluteUrl}/_api/web/lists/getbytitle('ProcessedDocs')/items?$select=Id,Title,FileRef&$top=10&$orderby=Created desc`;
      const response: SPHttpClientResponse = await this.props.httpClient.get(processedDocsUrl, SPHttpClient.configurations.v1);
      const data = await response.json();
      
      this.setState({
        enrichedDocuments: data.value || []
      });
    } catch (error) {
      // Silently fail - library might not exist yet
      console.log('Could not load enriched documents:', error);
    }
  }

  public componentDidMount(): void {
    this.loadEnrichedDocuments();
  }

  public render(): React.ReactElement<IDocumentUploaderProps> {
    return (
      <Stack tokens={{ childrenGap: 15 }} style={{ padding: '20px' }}>
        <Text variant="xLarge">SMEPilot - Document Enrichment</Text>
        
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
          <ProgressIndicator label="Uploading and enriching document..." />
        )}

        <Stack horizontal tokens={{ childrenGap: 10 }}>
          <input
            type="file"
            accept=".docx"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) {
                this.uploadFile(file);
              }
            }}
            style={{ display: 'none' }}
            id="file-upload-input"
          />
          <label htmlFor="file-upload-input">
            <PrimaryButton text="Upload Document (.docx)" />
          </label>
        </Stack>

        {this.state.enrichedDocuments.length > 0 && (
          <Stack tokens={{ childrenGap: 10 }}>
            <Text variant="mediumPlus">Recently Enriched Documents</Text>
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

