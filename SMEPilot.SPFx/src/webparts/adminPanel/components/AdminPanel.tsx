import * as React from 'react';
import { WebPartContext } from '@microsoft/sp-webpart-base';
import { SPHttpClient } from '@microsoft/sp-http';
import {
  MessageBar,
  MessageBarType,
  Stack,
  Text,
  TextField,
  ComboBox,
  IComboBoxOption,
  PrimaryButton,
  DefaultButton,
  Checkbox,
  Label,
  Spinner,
  SpinnerSize,
  Separator,
  Panel,
  PanelType,
  IconButton
} from '@fluentui/react';
import { SharePointService, IConfiguration as ISharePointConfiguration } from '../../../services/SharePointService';
import { FunctionAppService } from '../../../services/FunctionAppService';

export interface IAdminPanelProps {
  context: WebPartContext;
  functionAppUrl: string;
  httpClient: SPHttpClient;
}

export interface IConfiguration {
  sourceFolderPath: string;
  destinationFolderPath: string;
  templateFileUrl: string;
  maxFileSizeMB: number;
  processingTimeoutSeconds: number;
  maxRetries: number;
  copilotPrompt: string;
  accessTeams: boolean;
  accessWeb: boolean;
  accessO365: boolean;
}

export interface IAdminPanelState {
  // Configuration form state
  configuration: IConfiguration;
  // UI state
  isLoading: boolean;
  isSaving: boolean;
  error: string | null;
  success: string | null;
  // Validation errors
  validationErrors: { [key: string]: string };
  // Installation status
  isConfigured: boolean;
  // View/Edit mode
  isViewMode: boolean;
  // Configuration metadata
  lastUpdated: Date | null;
  subscriptionId: string | null;
  subscriptionExpiration: string | null;
  // Folder and file options
  folderOptions: IComboBoxOption[];
  templateFileOptions: IComboBoxOption[];
  isLoadingFolders: boolean;
  isLoadingTemplates: boolean;
  isUploadingTemplate: boolean;
  templateUploadFolder: string;
}

export default class AdminPanel extends React.Component<IAdminPanelProps, IAdminPanelState> {
  private sharePointService: SharePointService;
  private functionAppService: FunctionAppService;
  private defaultCopilotPrompt: string = `You are SMEPilot, an AI assistant that helps users find information from enriched functional and technical documents.

When users ask questions:
1. Analyze the enriched documents in the configured destination folder
2. Provide clear, concise answers based on the document content
3. Always include citations with source document links
4. If information is not found, politely inform the user
5. Use the document structure (Overview, Functional Details, Technical Details, Troubleshooting) to provide comprehensive answers

Format your responses:
- Start with a brief summary
- Provide numbered steps or bullet points when applicable
- Include relevant code snippets or examples from documents
- End with source citations

Remember: You can only access documents that the user has permission to view.`;

  constructor(props: IAdminPanelProps) {
    super(props);
    this.sharePointService = new SharePointService(props.context);
    this.functionAppService = new FunctionAppService(props.functionAppUrl);
    this.state = {
      configuration: {
        sourceFolderPath: '',
        destinationFolderPath: '',
        templateFileUrl: '',
        maxFileSizeMB: 50,
        processingTimeoutSeconds: 60,
        maxRetries: 3,
        copilotPrompt: this.defaultCopilotPrompt,
        accessTeams: true,
        accessWeb: true,
        accessO365: true
      },
      isLoading: true,
      isSaving: false,
      error: null,
      success: null,
      validationErrors: {},
      isConfigured: false,
      isViewMode: false,
      lastUpdated: null,
      subscriptionId: null,
      subscriptionExpiration: null,
      folderOptions: [],
      templateFileOptions: [],
      isLoadingFolders: false,
      isLoadingTemplates: false,
      isUploadingTemplate: false,
      templateUploadFolder: '/Shared Documents/Templates'
    };
  }

  public async componentDidMount(): Promise<void> {
    await this.loadConfiguration();
    await this.loadFoldersAndFiles();
  }

  private async loadFoldersAndFiles(): Promise<void> {
    // Load folders
    this.setState({ isLoadingFolders: true });
    try {
      const folders = await this.sharePointService.getFolders();
      const folderOptions: IComboBoxOption[] = folders.map(f => ({
        key: f.key,
        text: f.text
      }));
      this.setState({ folderOptions, isLoadingFolders: false });
    } catch (error: any) {
      console.error('Error loading folders:', error);
      this.setState({ isLoadingFolders: false });
    }

    // Load template files
    this.setState({ isLoadingTemplates: true });
    try {
      const files = await this.sharePointService.getTemplateFiles();
      const templateFileOptions: IComboBoxOption[] = files.map(f => ({
        key: f.key,
        text: f.text
      }));
      this.setState({ templateFileOptions, isLoadingTemplates: false });
    } catch (error: any) {
      console.error('Error loading template files:', error);
      this.setState({ isLoadingTemplates: false });
    }
  }

  private async handleTemplateUpload(file: File): Promise<void> {
    if (!file.name.toLowerCase().endsWith('.dotx')) {
      this.setState({
        error: 'Only .dotx template files are allowed',
        success: null
      });
      return;
    }

    this.setState({
      isUploadingTemplate: true,
      error: null,
      success: null
    });

    try {
      const targetFolder = this.state.templateUploadFolder || '/Shared Documents/Templates';
      const uploadedPath = await this.sharePointService.uploadTemplateFile(file, targetFolder);
      
      // Update the configuration with the uploaded file path
      this.handleInputChange('templateFileUrl', uploadedPath);
      
      // Reload template files to include the new one
      const files = await this.sharePointService.getTemplateFiles();
      const templateFileOptions: IComboBoxOption[] = files.map(f => ({
        key: f.key,
        text: f.text
      }));
      
      this.setState({
        templateFileOptions,
        isUploadingTemplate: false,
        success: `Template file "${file.name}" uploaded successfully to ${targetFolder}`,
        error: null
      });

      // Clear success message after 5 seconds
      setTimeout(() => {
        this.setState({ success: null });
      }, 5000);
    } catch (error: any) {
      this.setState({
        isUploadingTemplate: false,
        error: `Failed to upload template file: ${error.message}`,
        success: null
      });
    }
  }

  private async loadConfiguration(): Promise<void> {
    try {
      const config = await this.sharePointService.getConfiguration();
      
      if (config) {
        console.log('[AdminPanel] Configuration loaded:', config);
        console.log('[AdminPanel] Destination folder path from config:', config.destinationFolderPath);
        console.log('[AdminPanel] Available folder options:', this.state.folderOptions.map(f => ({ key: f.key, text: f.text })));
        
        // Ensure saved paths are in folderOptions so ComboBox can display them
        const updatedFolderOptions = [...this.state.folderOptions];
        const addPathIfMissing = (path: string, displayName?: string) => {
          if (!path) return;
          const exists = updatedFolderOptions.some(opt => opt.key === path);
          if (!exists) {
            // Extract a display name from the path
            const parts = path.split('/').filter(p => p);
            const name = displayName || parts[parts.length - 1] || path;
            updatedFolderOptions.push({
              key: path,
              text: name
            });
            console.log(`[AdminPanel] Added missing path to folderOptions: ${path} (${name})`);
          }
        };
        
        addPathIfMissing(config.sourceFolderPath);
        addPathIfMissing(config.destinationFolderPath);
        
        // Ensure saved template file is in templateFileOptions
        const updatedTemplateOptions = [...this.state.templateFileOptions];
        if (config.templateFileUrl && !updatedTemplateOptions.some(opt => opt.key === config.templateFileUrl)) {
          const parts = config.templateFileUrl.split('/').filter(p => p);
          const fileName = parts[parts.length - 1] || config.templateFileUrl;
          updatedTemplateOptions.push({
            key: config.templateFileUrl,
            text: fileName
          });
          console.log(`[AdminPanel] Added missing template file to templateFileOptions: ${config.templateFileUrl}`);
        }
        
        this.setState({
          configuration: {
            sourceFolderPath: config.sourceFolderPath || '',
            destinationFolderPath: config.destinationFolderPath || '',
            templateFileUrl: config.templateFileUrl || '',
            maxFileSizeMB: config.maxFileSizeMB || 50,
            processingTimeoutSeconds: config.processingTimeoutSeconds || 60,
            maxRetries: config.maxRetries || 3,
            copilotPrompt: config.copilotPrompt || this.defaultCopilotPrompt,
            accessTeams: config.accessTeams !== false,
            accessWeb: config.accessWeb !== false,
            accessO365: config.accessO365 !== false
          },
          folderOptions: updatedFolderOptions,
          templateFileOptions: updatedTemplateOptions,
          isConfigured: true,
          isLoading: false,
          isViewMode: true, // Start in view mode if configuration exists
          lastUpdated: config.lastUpdated || null,
          subscriptionId: config.subscriptionId || null,
          subscriptionExpiration: null // Will be loaded from config if available
        });
      } else {
        // No configuration found - show form
        this.setState({
          isLoading: false,
          isConfigured: false,
          isViewMode: false
        });
      }
    } catch (error: any) {
      // List doesn't exist yet - that's OK, show form
      this.setState({
        isLoading: false,
        isConfigured: false,
        isViewMode: false,
        error: null
      });
    }
  }

  private handleEditConfiguration = (): void => {
    this.setState({
      isViewMode: false,
      error: null,
      success: null
    });
  }

  private handleResetConfiguration = async (): Promise<void> => {
    if (!confirm('Are you sure you want to reset the configuration? This will clear all settings and require re-installation.')) {
      return;
    }

    this.setState({
      isSaving: true,
      error: null,
      success: null
    });

    try {
      // Reset to default values
      this.setState({
        configuration: {
          sourceFolderPath: '',
          destinationFolderPath: '',
          templateFileUrl: '',
          maxFileSizeMB: 50,
          processingTimeoutSeconds: 60,
          maxRetries: 3,
          copilotPrompt: this.defaultCopilotPrompt,
          accessTeams: true,
          accessWeb: true,
          accessO365: true
        },
        isConfigured: false,
        isViewMode: false,
        isSaving: false,
        lastUpdated: null,
        subscriptionId: null,
        subscriptionExpiration: null,
        success: 'Configuration reset. You can now configure from scratch.'
      });
    } catch (error: any) {
      this.setState({
        isSaving: false,
        error: `Failed to reset configuration: ${error.message}`
      });
    }
  }

  private validateConfiguration(): boolean {
    const errors: { [key: string]: string } = {};
    const config = this.state.configuration;

    // Source Folder validation
    if (!config.sourceFolderPath || config.sourceFolderPath.trim() === '') {
      errors.sourceFolderPath = 'Source Folder is required';
    }

    // Destination Folder validation
    if (!config.destinationFolderPath || config.destinationFolderPath.trim() === '') {
      errors.destinationFolderPath = 'Destination Folder is required';
    }

    // Template File validation
    if (!config.templateFileUrl || config.templateFileUrl.trim() === '') {
      errors.templateFileUrl = 'Template File is required';
    } else if (!config.templateFileUrl.toLowerCase().endsWith('.dotx')) {
      errors.templateFileUrl = 'Template file must be a .dotx file';
    }

    // Processing Settings validation
    if (config.maxFileSizeMB <= 0 || config.maxFileSizeMB > 1000) {
      errors.maxFileSizeMB = 'Max File Size must be between 1 and 1000 MB';
    }

    if (config.processingTimeoutSeconds <= 0 || config.processingTimeoutSeconds > 300) {
      errors.processingTimeoutSeconds = 'Timeout must be between 1 and 300 seconds';
    }

    if (config.maxRetries < 0 || config.maxRetries > 10) {
      errors.maxRetries = 'Retry count must be between 0 and 10';
    }

    // Copilot Prompt validation
    if (!config.copilotPrompt || config.copilotPrompt.trim() === '') {
      errors.copilotPrompt = 'Copilot Agent Prompt is required';
    }

    // At least one access point must be selected
    if (!config.accessTeams && !config.accessWeb && !config.accessO365) {
      errors.accessPoints = 'At least one access point must be selected';
    }

    this.setState({ validationErrors: errors });
    return Object.keys(errors).length === 0;
  }

  private handleInputChange = (field: keyof IConfiguration, value: any): void => {
    this.setState(prevState => ({
      configuration: {
        ...prevState.configuration,
        [field]: value
      },
      validationErrors: {
        ...prevState.validationErrors,
        [field]: '' // Clear error for this field
      },
      error: null,
      success: null
    }));
  }

  private handleSaveConfiguration = async (): Promise<void> => {
    // Validate
    if (!this.validateConfiguration()) {
      this.setState({
        error: 'Please fix validation errors before saving',
        success: null
      });
      return;
    }

    this.setState({
      isSaving: true,
      error: null,
      success: null
    });

    try {
      const config = this.state.configuration;
      const steps: string[] = [];

      // Step 1: Create SMEPilotConfig list
      steps.push('Creating SMEPilotConfig list...');
      await this.sharePointService.createSMEPilotConfigList();
      steps.push('✓ SMEPilotConfig list created');

      // Step 2: Save configuration to list
      steps.push('Saving configuration...');
      await this.sharePointService.saveConfiguration({
        sourceFolderPath: config.sourceFolderPath,
        destinationFolderPath: config.destinationFolderPath,
        templateFileUrl: config.templateFileUrl,
        maxFileSizeMB: config.maxFileSizeMB,
        processingTimeoutSeconds: config.processingTimeoutSeconds,
        maxRetries: config.maxRetries,
        copilotPrompt: config.copilotPrompt,
        accessTeams: config.accessTeams,
        accessWeb: config.accessWeb,
        accessO365: config.accessO365
      });
      steps.push('✓ Configuration saved');

      // Step 3: Create metadata columns
      steps.push('Creating metadata columns...');
      await this.sharePointService.createMetadataColumns(config.sourceFolderPath);
      steps.push('✓ Metadata columns created');

      // Step 4: Create error folders
      steps.push('Creating error folders...');
      await this.sharePointService.createErrorFolders(config.sourceFolderPath);
      steps.push('✓ Error folders created');

      // Step 5: Create webhook subscription
      steps.push('Creating webhook subscription...');
      const tenantId = this.props.context.pageContext.aadInfo?.tenantId?.toString() || '';
      
      // Get siteId and driveId for webhook subscription
      // Get driveId using SharePoint REST API (more reliable than Graph API in Function App)
      const siteId = this.sharePointService.getSiteId();
      const driveId = await this.sharePointService.getDriveIdFromFolderPath(config.sourceFolderPath);
      
      if (!driveId) {
        steps.push(`⚠ Could not get driveId from folder path: ${config.sourceFolderPath}`);
        // Continue anyway - Function App will try to resolve it
      }
      
      const webhookResult = await this.functionAppService.createWebhookSubscription({
        driveId: driveId || undefined, // Pass driveId if we got it, otherwise let Function App resolve it
        siteId: siteId || undefined,
        sourceFolderPath: config.sourceFolderPath,
        tenantId: tenantId,
        functionAppUrl: this.props.functionAppUrl,
        notificationUrl: `${this.props.functionAppUrl}/api/ProcessSharePointFile`
      });

      if (webhookResult.success && webhookResult.subscriptionId) {
        // Save subscription ID to configuration
        await this.sharePointService.saveConfiguration({
          ...config,
          subscriptionId: webhookResult.subscriptionId
        });
        steps.push(`✓ Webhook subscription created (ID: ${webhookResult.subscriptionId})`);
      } else {
        steps.push(`⚠ Webhook subscription failed: ${webhookResult.message || 'Unknown error'}`);
      }

      // Reload configuration to get updated metadata
      const updatedConfig = await this.sharePointService.getConfiguration();
      
      this.setState({
        isSaving: false,
        success: `Installation completed successfully!\n\n${steps.join('\n')}\n\nNext: Configure Copilot Agent in Copilot Studio.`,
        isConfigured: true,
        isViewMode: true, // Switch to view mode after saving
        lastUpdated: updatedConfig?.lastUpdated || new Date(),
        subscriptionId: webhookResult.subscriptionId || null
      });
    } catch (error: any) {
      this.setState({
        isSaving: false,
        error: `Failed to save configuration: ${error.message}\n\nPlease check:\n1. You have Site Collection Admin permissions\n2. Function App is accessible\n3. All folder paths are correct`
      });
    }
  }

  private handleTestConfiguration = async (): Promise<void> => {
    if (!this.validateConfiguration()) {
      this.setState({
        error: 'Please fix validation errors before testing',
        success: null
      });
      return;
    }

    this.setState({
      isLoading: true,
      error: null,
      success: null
    });

    try {
      const config = this.state.configuration;
      const validationResult = await this.sharePointService.validateConfiguration({
        sourceFolderPath: config.sourceFolderPath,
        destinationFolderPath: config.destinationFolderPath,
        templateFileUrl: config.templateFileUrl,
        maxFileSizeMB: config.maxFileSizeMB,
        processingTimeoutSeconds: config.processingTimeoutSeconds,
        maxRetries: config.maxRetries,
        copilotPrompt: config.copilotPrompt,
        accessTeams: config.accessTeams,
        accessWeb: config.accessWeb,
        accessO365: config.accessO365
      });

      if (validationResult.isValid) {
        this.setState({
          isLoading: false,
          success: 'Configuration validation passed! All settings are valid.\n\n✓ Source folder exists\n✓ Template file exists\n✓ All paths are accessible'
        });
      } else {
        this.setState({
          isLoading: false,
          error: `Configuration validation failed:\n\n${validationResult.errors.join('\n')}`
        });
      }
    } catch (error: any) {
      this.setState({
        isLoading: false,
        error: `Configuration validation failed: ${error.message}`
      });
    }
  }

  public render(): React.ReactElement<IAdminPanelProps> {
    if (this.state.isLoading && !this.state.isSaving) {
      return (
        <Stack tokens={{ childrenGap: 15 }} style={{ padding: '20px' }}>
          <Spinner size={SpinnerSize.large} label="Loading configuration..." />
        </Stack>
      );
    }

    const { configuration, validationErrors, error, success, isSaving, isConfigured, isViewMode, lastUpdated, subscriptionId } = this.state;

    return (
      <Stack tokens={{ childrenGap: 20 }} style={{ padding: '20px', maxWidth: '800px' }}>
        <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
          <Stack>
            <Text variant="xLarge" style={{ fontWeight: 600 }}>
              SMEPilot Installation Configuration
            </Text>
            <Text variant="medium" style={{ color: '#666' }}>
              {isConfigured 
                ? 'View and manage your SMEPilot configuration.' 
                : 'Configure all settings during installation. Both functionalities (Document Enrichment & Copilot Agent) will work immediately after configuration.'}
            </Text>
          </Stack>
          {isConfigured && isViewMode && (
            <Stack horizontal tokens={{ childrenGap: 10 }}>
              <DefaultButton
                text="Edit Configuration"
                onClick={this.handleEditConfiguration}
                iconProps={{ iconName: 'Edit' }}
              />
              <DefaultButton
                text="Reset"
                onClick={this.handleResetConfiguration}
                iconProps={{ iconName: 'Refresh' }}
                styles={{ root: { borderColor: '#d13438', color: '#d13438' } }}
              />
            </Stack>
          )}
        </Stack>

        {error && (
          <MessageBar messageBarType={MessageBarType.error} onDismiss={() => this.setState({ error: null })}>
            <div style={{ whiteSpace: 'pre-line' }}>{error}</div>
          </MessageBar>
        )}

        {success && (
          <MessageBar messageBarType={MessageBarType.success} onDismiss={() => this.setState({ success: null })}>
            <div style={{ whiteSpace: 'pre-line' }}>{success}</div>
          </MessageBar>
        )}

        {isConfigured && isViewMode && (
          <MessageBar messageBarType={MessageBarType.success}>
            Configuration is active and working. Click "Edit Configuration" to make changes.
          </MessageBar>
        )}

        {isConfigured && isViewMode && (
          <>
            <Separator />
            {/* View Configuration Section */}
            <Stack tokens={{ childrenGap: 15 }}>
              <Text variant="large" style={{ fontWeight: 600 }}>
                📋 Current Configuration
              </Text>
              
              <Stack tokens={{ childrenGap: 10 }} style={{ backgroundColor: '#f3f2f1', padding: '15px', borderRadius: '4px' }}>
                <Stack horizontal horizontalAlign="space-between">
                  <Text variant="medium" style={{ fontWeight: 600 }}>Last Updated:</Text>
                  <Text variant="medium">
                    {lastUpdated ? new Date(lastUpdated).toLocaleString() : 'Not available'}
                  </Text>
                </Stack>
                
                {subscriptionId && (
                  <Stack horizontal horizontalAlign="space-between">
                    <Text variant="medium" style={{ fontWeight: 600 }}>Webhook Subscription:</Text>
                    <Text variant="medium" style={{ color: '#107c10' }}>
                      ✓ Active (ID: {subscriptionId.substring(0, 20)}...)
                    </Text>
                  </Stack>
                )}
                
                <Separator />
                
                <Stack tokens={{ childrenGap: 8 }}>
                  <Text variant="medium" style={{ fontWeight: 600 }}>Source Folder:</Text>
                  <Text variant="small" style={{ marginLeft: '15px' }}>{configuration.sourceFolderPath || 'Not set'}</Text>
                  
                  <Text variant="medium" style={{ fontWeight: 600 }}>Destination Folder:</Text>
                  <Text variant="small" style={{ marginLeft: '15px' }}>{configuration.destinationFolderPath || 'Not set'}</Text>
                  
                  <Text variant="medium" style={{ fontWeight: 600 }}>Template File:</Text>
                  <Text variant="small" style={{ marginLeft: '15px' }}>{configuration.templateFileUrl || 'Not set'}</Text>
                  
                  <Text variant="medium" style={{ fontWeight: 600 }}>Processing Settings:</Text>
                  <Text variant="small" style={{ marginLeft: '15px' }}>
                    Max Size: {configuration.maxFileSizeMB}MB | 
                    Timeout: {configuration.processingTimeoutSeconds}s | 
                    Retries: {configuration.maxRetries}
                  </Text>
                  
                  <Text variant="medium" style={{ fontWeight: 600 }}>Access Points:</Text>
                  <Text variant="small" style={{ marginLeft: '15px' }}>
                    {[
                      configuration.accessTeams && 'Teams',
                      configuration.accessWeb && 'Web',
                      configuration.accessO365 && 'O365'
                    ].filter(Boolean).join(', ') || 'None'}
                  </Text>
                </Stack>
              </Stack>
            </Stack>
            <Separator />
          </>
        )}

        {!isViewMode && <Separator />}

        {/* PART 1: Document Enrichment Configuration */}
        <Stack tokens={{ childrenGap: 15 }}>
          <Text variant="large" style={{ fontWeight: 600, color: '#0078d4' }}>
            📄 Part 1: Document Enrichment Configuration
          </Text>

          {/* Source Folder */}
          <Stack tokens={{ childrenGap: 4 }}>
            <Label required>Source Folder (User Selected) *</Label>
            <Text variant="small" style={{ color: '#666', marginBottom: '4px' }}>
              Where users upload raw documents (.docx files). Folder must exist and be accessible.
            </Text>
            <ComboBox
              options={this.state.folderOptions}
              selectedKey={configuration.sourceFolderPath}
              text={configuration.sourceFolderPath || undefined}
              onChange={(e, option) => {
                if (option) {
                  this.handleInputChange('sourceFolderPath', option.key as string);
                }
              }}
              onInputValueChange={(newValue) => {
                // Allow free text input as well
                this.handleInputChange('sourceFolderPath', newValue);
              }}
              allowFreeform
              autoComplete="on"
              errorMessage={validationErrors.sourceFolderPath}
              placeholder="Select or type folder path..."
              disabled={isSaving || isViewMode}
              onRenderOption={(option) => {
                return <div style={{ padding: '4px 0' }}>{option?.text}</div>;
              }}
            />
          </Stack>
          {this.state.isLoadingFolders && (
            <Stack horizontal tokens={{ childrenGap: 8 }} verticalAlign="center">
              <Spinner size={SpinnerSize.small} />
              <Text variant="small">Loading folders...</Text>
            </Stack>
          )}

          {/* Destination Folder */}
          <Stack tokens={{ childrenGap: 4 }}>
            <Label required>Destination Folder (User Selected) *</Label>
            <Text variant="small" style={{ color: '#666', marginBottom: '4px' }}>
              Where enriched documents are stored. Folder will be created if it doesn't exist.
            </Text>
            <ComboBox
              options={this.state.folderOptions}
              selectedKey={configuration.destinationFolderPath}
              text={configuration.destinationFolderPath || undefined}
              onChange={(e, option) => {
                if (option) {
                  this.handleInputChange('destinationFolderPath', option.key as string);
                }
              }}
              onInputValueChange={(newValue) => {
                // Allow free text input as well
                this.handleInputChange('destinationFolderPath', newValue);
              }}
              allowFreeform
              autoComplete="on"
              errorMessage={validationErrors.destinationFolderPath}
              placeholder="Select or type folder path..."
              disabled={isSaving || isViewMode}
              onRenderOption={(option) => {
                return <div style={{ padding: '4px 0' }}>{option?.text}</div>;
              }}
            />
          </Stack>

          {/* Template File */}
          <Stack tokens={{ childrenGap: 8 }}>
            <Stack tokens={{ childrenGap: 4 }}>
              <Label required>Template File *</Label>
              <Text variant="small" style={{ color: '#666', marginBottom: '4px' }}>
                Company template file (.dotx) for document formatting. File must exist and be accessible.
              </Text>
              <Stack horizontal tokens={{ childrenGap: 10 }} verticalAlign="end">
                <Stack.Item grow>
                  <ComboBox
                    options={this.state.templateFileOptions}
                    selectedKey={configuration.templateFileUrl}
                    text={configuration.templateFileUrl || undefined}
                    onChange={(e, option) => {
                      if (option) {
                        this.handleInputChange('templateFileUrl', option.key as string);
                      } else {
                        // Clear selection
                        this.handleInputChange('templateFileUrl', '');
                      }
                    }}
                    onInputValueChange={(newValue) => {
                      // Allow free text input as well
                      this.handleInputChange('templateFileUrl', newValue);
                    }}
                    allowFreeform
                    autoComplete="on"
                    errorMessage={validationErrors.templateFileUrl}
                    placeholder="Select or type template file path..."
                    disabled={isSaving || isViewMode || this.state.isUploadingTemplate}
                    onRenderOption={(option) => {
                      return <div style={{ padding: '4px 0' }}>{option?.text}</div>;
                    }}
                  />
                </Stack.Item>
                {!isViewMode && !configuration.templateFileUrl && (
                  <DefaultButton
                    text="Upload Template"
                    iconProps={{ iconName: 'Upload' }}
                    onClick={() => {
                      const input = document.createElement('input');
                      input.type = 'file';
                      input.accept = '.dotx';
                      input.onchange = async (e: any) => {
                        const file = e.target.files?.[0];
                        if (file) {
                          await this.handleTemplateUpload(file);
                        }
                      };
                      input.click();
                    }}
                    disabled={isSaving || this.state.isUploadingTemplate}
                    styles={{
                      root: {
                        minWidth: '140px'
                      }
                    }}
                  />
                )}
                {!isViewMode && configuration.templateFileUrl && (
                  <DefaultButton
                    text="Change Template"
                    iconProps={{ iconName: 'Edit' }}
                    onClick={() => {
                      this.handleInputChange('templateFileUrl', '');
                    }}
                    disabled={isSaving || this.state.isUploadingTemplate}
                    styles={{
                      root: {
                        minWidth: '140px'
                      }
                    }}
                  />
                )}
              </Stack>
              {this.state.isUploadingTemplate && (
                <Stack horizontal tokens={{ childrenGap: 8 }} verticalAlign="center">
                  <Spinner size={SpinnerSize.small} />
                  <Text variant="small">Uploading template file...</Text>
                </Stack>
              )}
            </Stack>
            {!isViewMode && !configuration.templateFileUrl && (
              <Stack tokens={{ childrenGap: 4 }} style={{ padding: '12px', backgroundColor: '#f3f2f1', borderRadius: '4px', border: '1px solid #edebe9' }}>
                <Text variant="small" style={{ fontWeight: 600, marginBottom: '4px' }}>📁 Upload to folder:</Text>
                <ComboBox
                  options={this.state.folderOptions}
                  selectedKey={this.state.templateUploadFolder}
                  onChange={(e, option) => {
                    if (option) {
                      this.setState({ templateUploadFolder: option.key as string });
                    }
                  }}
                  onInputValueChange={(newValue) => {
                    this.setState({ templateUploadFolder: newValue });
                  }}
                  allowFreeform
                  placeholder="Select folder for upload..."
                  disabled={isSaving || this.state.isUploadingTemplate}
                  styles={{ root: { maxWidth: '400px' } }}
                />
                <Text variant="small" style={{ color: '#666', marginTop: '4px' }}>
                  Select the folder where you want to upload the template file. Default: /Shared Documents/Templates
                </Text>
              </Stack>
            )}
          </Stack>
          {this.state.isLoadingTemplates && (
            <Stack horizontal tokens={{ childrenGap: 8 }} verticalAlign="center">
              <Spinner size={SpinnerSize.small} />
              <Text variant="small">Loading template files...</Text>
            </Stack>
          )}
        </Stack>

        <Separator />

        {/* PART 2: Copilot Agent Configuration */}
        <Stack tokens={{ childrenGap: 15 }}>
          <Text variant="large" style={{ fontWeight: 600, color: '#c2185b' }}>
            🤖 Part 2: Copilot Agent Configuration
          </Text>

          <MessageBar messageBarType={MessageBarType.info}>
            Copilot Agent is always enabled. Users can access it via the selected access points below.
          </MessageBar>

          {/* Copilot Prompt */}
          <TextField
            label="Copilot Agent Prompt"
            description="Custom instructions for the Copilot Agent. This prompt will be used to analyze enriched documents and provide answers."
            value={configuration.copilotPrompt}
            onChange={(e, value) => this.handleInputChange('copilotPrompt', value || '')}
            errorMessage={validationErrors.copilotPrompt}
            multiline
            rows={10}
            required
            disabled={isSaving || isViewMode}
            readOnly={isViewMode}
          />

          {/* Access Points */}
          <Stack tokens={{ childrenGap: 10 }}>
            <Label required>Access Points (Where Users Can Access Copilot Agent)</Label>
            <Text variant="small" style={{ color: '#666', marginBottom: '8px' }}>
              Select where users can access the Copilot Agent. At least one must be selected.
            </Text>
            {validationErrors.accessPoints && (
              <Text variant="small" style={{ color: '#a80000' }}>
                {validationErrors.accessPoints}
              </Text>
            )}
            <Checkbox
              label="Microsoft Teams"
              checked={configuration.accessTeams}
              onChange={(e, checked) => this.handleInputChange('accessTeams', checked || false)}
              disabled={isSaving || isViewMode}
            />
            <Checkbox
              label="Web Interface (SharePoint Portal)"
              checked={configuration.accessWeb}
              onChange={(e, checked) => this.handleInputChange('accessWeb', checked || false)}
              disabled={isSaving || isViewMode}
            />
            <Checkbox
              label="O365 Copilot (Word, Excel, PPT, Office Apps)"
              checked={configuration.accessO365}
              onChange={(e, checked) => this.handleInputChange('accessO365', checked || false)}
              disabled={isSaving || isViewMode}
            />
          </Stack>
        </Stack>

        <Separator />

        {/* PART 3: Processing Settings */}
        <Stack tokens={{ childrenGap: 15 }}>
          <Text variant="large" style={{ fontWeight: 600, color: '#e65100' }}>
            ⚙️ Part 3: Processing Settings
          </Text>

          <Stack horizontal tokens={{ childrenGap: 20 }}>
            <TextField
              label="Max File Size (MB)"
              description="Files larger than this will be rejected"
              value={configuration.maxFileSizeMB.toString()}
              onChange={(e, value) => {
                const numValue = parseInt(value || '50', 10);
                if (!isNaN(numValue)) {
                  this.handleInputChange('maxFileSizeMB', numValue);
                }
              }}
              errorMessage={validationErrors.maxFileSizeMB}
              type="number"
              required
              disabled={isSaving || isViewMode}
              readOnly={isViewMode}
              styles={{ root: { width: '200px' } }}
            />

            <TextField
              label="Processing Timeout (seconds)"
              description="Maximum time to process a file"
              value={configuration.processingTimeoutSeconds.toString()}
              onChange={(e, value) => {
                const numValue = parseInt(value || '60', 10);
                if (!isNaN(numValue)) {
                  this.handleInputChange('processingTimeoutSeconds', numValue);
                }
              }}
              errorMessage={validationErrors.processingTimeoutSeconds}
              type="number"
              required
              disabled={isSaving || isViewMode}
              readOnly={isViewMode}
              styles={{ root: { width: '200px' } }}
            />

            <TextField
              label="Max Retries"
              description="Number of retry attempts for failed processing"
              value={configuration.maxRetries.toString()}
              onChange={(e, value) => {
                const numValue = parseInt(value || '3', 10);
                if (!isNaN(numValue)) {
                  this.handleInputChange('maxRetries', numValue);
                }
              }}
              errorMessage={validationErrors.maxRetries}
              type="number"
              required
              disabled={isSaving || isViewMode}
              readOnly={isViewMode}
              styles={{ root: { width: '200px' } }}
            />
          </Stack>
        </Stack>

        <Separator />

        {/* Action Buttons */}
        {!isViewMode && (
          <Stack horizontal tokens={{ childrenGap: 10 }}>
            <PrimaryButton
              text="Save Configuration"
              onClick={this.handleSaveConfiguration}
              disabled={isSaving}
              iconProps={isSaving ? undefined : { iconName: 'Save' }}
            />
            <DefaultButton
              text="Test Configuration"
              onClick={this.handleTestConfiguration}
              disabled={isSaving || this.state.isLoading}
              iconProps={{ iconName: 'CheckMark' }}
            />
            {isConfigured && (
              <DefaultButton
                text="Cancel"
                onClick={() => {
                  this.loadConfiguration(); // Reload to reset to view mode
                }}
                disabled={isSaving}
              />
            )}
          </Stack>
        )}

        {isSaving && (
          <Stack horizontal tokens={{ childrenGap: 10 }} verticalAlign="center">
            <Spinner size={SpinnerSize.small} />
            <Text>Saving configuration...</Text>
          </Stack>
        )}
      </Stack>
    );
  }
}


