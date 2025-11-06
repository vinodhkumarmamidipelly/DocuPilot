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

export default class DocumentUploaderWebPart extends BaseClientSideWebPart<IDocumentUploaderWebPartProps> {
  private _functionAppUrl: string = '';

  protected onInit(): Promise<void> {
    return super.onInit().then(_ => {
      // Default to ngrok URL for local testing, or use configured value
      this._functionAppUrl = this.properties.functionAppUrl || 
        'https://562fbad9f946.ngrok-free.app'; // Update this with your ngrok URL for local testing
    });
  }

  public render(): void {
    const element: React.ReactElement<IDocumentUploaderProps> = React.createElement(
      DocumentUploader,
      {
        context: this.context,
        libraryName: this.properties.scratchDocsLibrary || 'ScratchDocs',
        functionAppUrl: this._functionAppUrl,
        httpClient: this.context.spHttpClient
      }
    );

    ReactDom.render(element, this.domElement);
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
                  value: ''
                })
              ]
            }
          ]
        }
      ]
    };
  }
}

