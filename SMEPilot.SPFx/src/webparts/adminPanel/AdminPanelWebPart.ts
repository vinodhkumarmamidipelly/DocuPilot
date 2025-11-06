import * as React from 'react';
import * as ReactDom from 'react-dom';
import { Version } from '@microsoft/sp-core-library';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';
import AdminPanel from './components/AdminPanel';
import { IAdminPanelProps } from './components/AdminPanel';

export interface IAdminPanelWebPartProps {
  functionAppUrl: string;
}

export default class AdminPanelWebPart extends BaseClientSideWebPart<IAdminPanelWebPartProps> {
  public render(): void {
    const element: React.ReactElement<IAdminPanelProps> = React.createElement(
      AdminPanel,
      {
        context: this.context,
        functionAppUrl: this.properties.functionAppUrl || 'https://your-function-app.azurewebsites.net',
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
}


