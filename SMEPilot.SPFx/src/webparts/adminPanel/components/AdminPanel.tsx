import * as React from 'react';
import { WebPartContext } from '@microsoft/sp-webpart-base';
import { SPHttpClient } from '@microsoft/sp-http';
import {
  MessageBar,
  MessageBarType,
  Stack,
  Text,
  DetailsList,
  IColumn,
  SelectionMode
} from '@fluentui/react';

export interface IAdminPanelProps {
  context: WebPartContext;
  functionAppUrl: string;
  httpClient: SPHttpClient;
}

export interface IAdminPanelState {
  enrichmentLogs: any[];
  isLoading: boolean;
  error: string | null;
}

export default class AdminPanel extends React.Component<IAdminPanelProps, IAdminPanelState> {
  constructor(props: IAdminPanelProps) {
    super(props);
    this.state = {
      enrichmentLogs: [],
      isLoading: true,
      error: null
    };
  }

  public async componentDidMount(): Promise<void> {
    await this.loadEnrichmentLogs();
  }

  private async loadEnrichmentLogs(): Promise<void> {
    try {
      // Load enrichment history from SharePoint metadata or Cosmos DB
      // For now, check ProcessedDocs library
      const url = `${this.props.context.pageContext.web.absoluteUrl}/_api/web/lists/getbytitle('ProcessedDocs')/items?$select=Id,Title,SMEPilot_Status,SMEPilot_EnrichedFileUrl,Modified&$orderby=Modified desc&$top=50`;
      const response = await this.props.httpClient.get(url, SPHttpClient.configurations.v1);
      
      if (!response.ok) {
        // If list doesn't exist (404), that's OK - no logs yet
        if (response.status === 404) {
          console.log('ProcessedDocs library not found - no enrichment logs yet');
          this.setState({
            enrichmentLogs: [],
            isLoading: false,
            error: null
          });
          return;
        }
        throw new Error(`Failed to load enrichment logs: ${response.status}`);
      }
      
      const data = await response.json();
      
      this.setState({
        enrichmentLogs: data.value || [],
        isLoading: false,
        error: null
      });
    } catch (error: any) {
      this.setState({
        error: error.message,
        isLoading: false
      });
    }
  }

  public render(): React.ReactElement<IAdminPanelProps> {
    const columns: IColumn[] = [
      { key: 'title', name: 'Document', fieldName: 'Title', minWidth: 200 },
      { key: 'status', name: 'Status', fieldName: 'SMEPilot_Status', minWidth: 100 },
      { key: 'modified', name: 'Last Modified', fieldName: 'Modified', minWidth: 150 }
    ];

    return (
      <Stack tokens={{ childrenGap: 15 }} style={{ padding: '20px' }}>
        <Text variant="xLarge">SMEPilot Admin Panel</Text>
        
        {this.state.error && (
          <MessageBar messageBarType={MessageBarType.error}>
            {this.state.error}
          </MessageBar>
        )}

        <Stack tokens={{ childrenGap: 10 }}>
          <Text variant="mediumPlus">Enrichment History</Text>
          {this.state.isLoading ? (
            <Text>Loading...</Text>
          ) : (
            <DetailsList
              items={this.state.enrichmentLogs}
              columns={columns}
              selectionMode={SelectionMode.none}
            />
          )}
        </Stack>
      </Stack>
    );
  }
}


