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
    const response = await fetch(`${this.functionAppUrl}/api/ProcessSharePointFile`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Enrichment failed: ${errorText}`);
    }

    return await response.json();
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


