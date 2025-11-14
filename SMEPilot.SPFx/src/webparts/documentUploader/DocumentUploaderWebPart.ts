import * as React from 'react';
import * as ReactDom from 'react-dom';
import { Version } from '@microsoft/sp-core-library';
import {
  IPropertyPaneConfiguration,
  PropertyPaneTextField
} from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import * as strings from 'DocumentUploaderWebPartStrings';
import DocumentUploader from './components/DocumentUploader';
import { IDocumentUploaderProps } from './components/DocumentUploader';

export interface IDocumentUploaderWebPartProps {
  scratchDocsLibrary: string;
  functionAppUrl: string;
}

class DocumentUploaderWebPart extends BaseClientSideWebPart<IDocumentUploaderWebPartProps> {
  private _functionAppUrl: string = '';


  protected onInit(): Promise<void> {
    return super.onInit().then(_ => {
      // Use configured value, or default to ngrok URL for local testing
      // For production, configure Azure Function App URL via web part properties
      this._functionAppUrl = this.properties.functionAppUrl || 
        'https://078dcba0929b.ngrok-free.app'; // Ngrok URL for local Function App testing
    });
  }

  public render(): void {
    try {
      const element: React.ReactElement<IDocumentUploaderProps> = React.createElement(
        DocumentUploader,
        {
          context: this.context,
          libraryName: this.properties.scratchDocsLibrary || 'ScratchDocs',
          functionAppUrl: this._functionAppUrl,
          httpClient: this.context.spHttpClient
        }
      );

      // Use React 17 render
      ReactDom.render(element, this.domElement);
    } catch (error: any) {
      console.error('Error rendering DocumentUploader:', error);
      const errorMessage = error?.message || error?.toString() || 'Unknown error';
      const errorStack = error?.stack || '';
      this.domElement.innerHTML = `
        <div style="padding: 20px; border: 2px solid red; background: #fff5f5;">
          <h3 style="color: red; margin-top: 0;">Error loading web part</h3>
          <p><strong>Error:</strong> ${errorMessage}</p>
          ${errorStack ? `<pre style="font-size: 11px; overflow: auto;">${errorStack}</pre>` : ''}
        </div>
      `;
    }
  }

  protected onDispose(): void {
    ReactDom.unmountComponentAtNode(this.domElement);
  }

  protected get dataVersion(): Version {
    return Version.parse('1.0');
  }

  protected getPropertyPaneConfiguration(): IPropertyPaneConfiguration {
    return {
      pages: [
        {
          header: {
            description: strings.PropertyPaneDescription
          },
          groups: [
            {
              groupName: strings.BasicGroupName,
              groupFields: [
                PropertyPaneTextField('scratchDocsLibrary', {
                  label: 'ScratchDocs Library Name',
                  value: 'ScratchDocs'
                }),
                PropertyPaneTextField('functionAppUrl', {
                  label: 'Function App URL',
                  description: 'Enter your Azure Function App URL (e.g., https://your-app.azurewebsites.net) or ngrok URL for local testing',
                  value: this.properties.functionAppUrl || ''
                })
              ]
            }
          ]
        }
      ]
    };
  }
}

export default DocumentUploaderWebPart;

