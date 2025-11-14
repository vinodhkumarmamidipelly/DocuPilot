import * as React from 'react';
import * as ReactDom from 'react-dom';
import { Version } from '@microsoft/sp-core-library';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';
import AdminPanel from './components/AdminPanel';
import { IAdminPanelProps } from './components/AdminPanel';

export interface IAdminPanelWebPartProps {
  functionAppUrl: string;
}

class AdminPanelWebPart extends BaseClientSideWebPart<IAdminPanelWebPartProps> {


  public render(): void {
    try {
      const element: React.ReactElement<IAdminPanelProps> = React.createElement(
        AdminPanel,
        {
          context: this.context,
          functionAppUrl: this.properties.functionAppUrl || 'https://078dcba0929b.ngrok-free.app',
          httpClient: this.context.spHttpClient
        }
      );

      // Use React 17 render
      ReactDom.render(element, this.domElement);
    } catch (error: any) {
      console.error('Error rendering AdminPanel:', error);
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
}

export default AdminPanelWebPart;


