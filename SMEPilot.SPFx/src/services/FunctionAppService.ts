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

export interface QueryAnswerRequest {
  question: string;
  tenantId?: string;
}

export interface QueryAnswerResponse {
  answer: string;
  sources: Array<{
    FileUrl: string;
    Heading: string;
    score: number;
  }>;
}

export class FunctionAppService {
  private functionAppUrl: string;

  constructor(functionAppUrl: string) {
    this.functionAppUrl = functionAppUrl;
  }

  /**
   * Trigger document enrichment via Function App
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
        throw new Error(`Enrichment failed (${response.status}): ${errorText}`);
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
   * Query enriched documents
   */
  async queryAnswer(request: QueryAnswerRequest, accessToken?: string): Promise<QueryAnswerResponse> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json'
    };

    if (accessToken) {
      headers['Authorization'] = `Bearer ${accessToken}`;
    }

    const response = await fetch(`${this.functionAppUrl}/api/QueryAnswer`, {
      method: 'POST',
      headers,
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Query failed: ${errorText}`);
    }

    return await response.json();
  }
}


