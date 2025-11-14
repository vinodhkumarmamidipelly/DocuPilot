/**
 * Service for calling Azure Function App APIs
 */

export interface ProcessSharePointFileRequest {
  siteId: string;
  driveId: string;
  itemId: string;
  fileName: string;
  uploaderEmail: string;
  tenantId?: string;
}

export interface ProcessSharePointFileResponse {
  enrichedUrl: string;
}

export interface WebhookSubscriptionRequest {
  sourceFolderPath?: string;
  driveId?: string;
  siteId?: string;
  tenantId?: string;
  functionAppUrl?: string;
  notificationUrl?: string;
}

export interface WebhookSubscriptionResponse {
  subscriptionId: string;
  expirationDateTime: string;
  success: boolean;
  message?: string;
}

export class FunctionAppService {
  private functionAppUrl: string;

  constructor(functionAppUrl: string) {
    this.functionAppUrl = functionAppUrl;
  }

  /**
   * Trigger document template formatting via Function App
   */
  async processSharePointFile(request: ProcessSharePointFileRequest): Promise<ProcessSharePointFileResponse> {
    try {
      const response = await fetch(`${this.functionAppUrl}/api/ProcessSharePointFile`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(request),
        // Add mode and credentials for CORS
        mode: 'cors',
        credentials: 'omit'
      });

      // Check if response is actually from ngrok warning page
      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('text/html')) {
        const text = await response.text();
        if (text.includes('ngrok') || text.includes('browser warning')) {
          throw new Error('Ngrok browser warning page detected. Please visit the ngrok URL in a new tab and click "Visit Site" to bypass the warning, then try again.');
        }
      }

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Template formatting failed (${response.status}): ${errorText}`);
      }

      return await response.json();
    } catch (error: any) {
      // Enhanced error handling for CORS issues
      if (error.message.includes('Failed to fetch') || error.message.includes('CORS')) {
        throw new Error(`CORS error: Cannot connect to Function App. Please ensure:
1. Function App is running locally
2. Ngrok tunnel is active (if using ngrok)
3. Visit ${this.functionAppUrl} in a new tab and bypass ngrok warning (if shown)
4. CORS headers are configured in Function App`);
      }
      throw error;
    }
  }

  /**
   * Create webhook subscription for monitoring source folder
   */
  async createWebhookSubscription(request: WebhookSubscriptionRequest): Promise<WebhookSubscriptionResponse> {
    try {
      // Build request body - only include non-null/non-undefined values
      // This prevents sending "null" as a string
      const requestBody: any = {};
      
      if (request.driveId && request.driveId !== 'null' && request.driveId !== 'undefined') {
        requestBody.driveId = request.driveId;
      }
      if (request.siteId && request.siteId !== 'null' && request.siteId !== 'undefined') {
        requestBody.siteId = request.siteId;
      }
      if (request.sourceFolderPath) {
        requestBody.sourceFolderPath = request.sourceFolderPath;
      }
      if (request.notificationUrl) {
        requestBody.notificationUrl = request.notificationUrl;
      } else {
        requestBody.notificationUrl = `${this.functionAppUrl}/api/ProcessSharePointFile`;
      }
      if (request.functionAppUrl) {
        requestBody.functionAppUrl = request.functionAppUrl;
      }
      if (request.tenantId) {
        requestBody.tenantId = request.tenantId;
      }
      
      console.log('[FunctionAppService] Creating webhook subscription with body:', JSON.stringify(requestBody, null, 2));

      const response = await fetch(`${this.functionAppUrl}/api/SetupSubscription`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(requestBody),
        mode: 'cors',
        credentials: 'omit'
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to create webhook subscription (${response.status}): ${errorText}`);
      }

      const result = await response.json();
      return {
        subscriptionId: result.subscriptionId || '',
        expirationDateTime: result.expirationDateTime || '',
        success: true,
        message: result.message
      };
    } catch (error: any) {
      console.error('Error creating webhook subscription:', error);
      return {
        subscriptionId: '',
        expirationDateTime: '',
        success: false,
        message: error.message
      };
    }
  }

  /**
   * Validate webhook subscription
   */
  async validateWebhookSubscription(subscriptionId: string): Promise<boolean> {
    try {
      const response = await fetch(`${this.functionAppUrl}/api/ValidateSubscription?subscriptionId=${subscriptionId}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json'
        },
        mode: 'cors',
        credentials: 'omit'
      });

      if (!response.ok) {
        return false;
      }

      const result = await response.json();
      return result.isValid === true;
    } catch (error: any) {
      console.error('Error validating webhook subscription:', error);
      return false;
    }
  }

}


