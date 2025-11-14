/**
 * SharePoint Service for SMEPilot Configuration Management
 * Handles creation of SMEPilotConfig list, saving/loading configuration,
 * creating metadata columns, and error folders
 */

import { SPHttpClient, SPHttpClientResponse, ISPHttpClientOptions } from '@microsoft/sp-http';
import { WebPartContext } from '@microsoft/sp-webpart-base';
import { MSGraphClientV3 } from '@microsoft/sp-http';

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
  subscriptionId?: string;
  lastUpdated?: Date;
}

export interface IValidationResult {
  isValid: boolean;
  errors: string[];
}

export class SharePointService {
  private context: WebPartContext;
  private httpClient: SPHttpClient;
  private webUrl: string;
  private listName: string = 'SMEPilotConfig';

  constructor(context: WebPartContext) {
    this.context = context;
    this.httpClient = context.spHttpClient;
    this.webUrl = context.pageContext.web.absoluteUrl;
  }

  /**
   * Create SMEPilotConfig list with all required columns
   */
  public async createSMEPilotConfigList(): Promise<boolean> {
    try {
      // Check if list already exists
      const listExists = await this.listExists();
      if (listExists) {
        return true;
      }

      // Get request digest
      const digestUrl = `${this.webUrl}/_api/contextinfo`;
      const digestResponse = await this.httpClient.post(
        digestUrl,
        SPHttpClient.configurations.v1,
        {
          body: '' // Empty body for contextinfo
        }
      );

      if (!digestResponse.ok) {
        const errorText = await digestResponse.text();
        throw new Error(`Failed to get request digest (${digestResponse.status}): ${errorText}`);
      }

      const digestData = await digestResponse.json();
      const digest = digestData.d?.GetContextWebInformation?.FormDigestValue || 
                     digestData.FormDigestValue || 
                     '';

      if (!digest) {
        throw new Error('Request digest not found in response');
      }

      // Create the list
      const createListUrl = `${this.webUrl}/_api/web/lists`;
      const listBody = {
        Title: this.listName,
        Description: 'SMEPilot Configuration List - Stores all installation-time settings',
        BaseTemplate: 100, // Custom List
        ContentTypesEnabled: false,
        Hidden: false
      };

      const createResponse = await this.httpClient.post(
        createListUrl,
        SPHttpClient.configurations.v1,
        {
          headers: {
            'X-RequestDigest': digest
          },
          body: JSON.stringify(listBody)
        }
      );

      if (!createResponse.ok) {
        const errorText = await createResponse.text();
        throw new Error(`Failed to create list (${createResponse.status}): ${errorText}`);
      }

      const listData = await createResponse.json();
      const listId = listData.d?.Id || listData.Id;
      
      await this.addListColumns(listId);
      return true;
    } catch (error: any) {
      console.error('Error creating SMEPilotConfig list:', error);
      throw error;
    }
  }

  /**
   * Check if SMEPilotConfig list exists
   */
  private async listExists(): Promise<boolean> {
    try {
      const url = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      return response.ok;
    } catch {
      return false;
    }
  }

  /**
   * Get request digest for POST operations
   */
  private async getRequestDigest(): Promise<string> {
    const digestUrl = `${this.webUrl}/_api/contextinfo`;
    const digestResponse = await this.context.spHttpClient.post(
      digestUrl,
      SPHttpClient.configurations.v1,
      {
        body: '' // Empty body for contextinfo - SPHttpClient will set correct Accept automatically
      }
    );

    if (!digestResponse.ok) {
      const errorText = await digestResponse.text();
      throw new Error(`Failed to get request digest (${digestResponse.status}): ${errorText}`);
    }

    const digestData = await digestResponse.json();
    // Handle both response formats (verbose with 'd' wrapper or nometadata flat)
    const digest = digestData?.GetContextWebInformation?.FormDigestValue ||
                   digestData?.d?.GetContextWebInformation?.FormDigestValue ||
                   digestData?.FormDigestValue ||
                   '';
    
    if (!digest) {
      throw new Error('Request digest not found in response');
    }

    return digest;
  }

  /**
   * Get the list item type name for the SMEPilotConfig list
   */
  private async getListItemTypeName(): Promise<string> {
    try {
      const url = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')?$select=ListItemEntityTypeFullName`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      
      if (response.ok) {
        const data = await response.json();
        return data.d?.ListItemEntityTypeFullName || `SP.Data.${this.listName}ListItem`;
      }
    } catch (error) {
      console.warn('Error getting list item type name:', error);
    }
    
    // Fallback to default format
    return `SP.Data.${this.listName}ListItem`;
  }

  /**
   * Add all required columns to the list using CreateFieldAsXml
   */
  private async addListColumns(listId: string): Promise<void> {
    // Define all fields using XML schema (this ensures explicit internal names and MaxLength)
    const fieldsXml = [
      `<Field Type='Text' Name='SourceFolderPath' StaticName='SourceFolderPath' DisplayName='SourceFolderPath' MaxLength='1024' Required='TRUE' />`,
      `<Field Type='Text' Name='DestinationFolderPath' StaticName='DestinationFolderPath' DisplayName='DestinationFolderPath' MaxLength='1024' Required='TRUE' />`,
      `<Field Type='Text' Name='TemplateFileUrl' StaticName='TemplateFileUrl' DisplayName='TemplateFileUrl' MaxLength='1024' Required='TRUE' />`,
      `<Field Type='Number' Name='MaxFileSizeMB' StaticName='MaxFileSizeMB' DisplayName='MaxFileSizeMB' />`,
      `<Field Type='Number' Name='ProcessingTimeoutSeconds' StaticName='ProcessingTimeoutSeconds' DisplayName='ProcessingTimeoutSeconds' />`,
      `<Field Type='Number' Name='MaxRetries' StaticName='MaxRetries' DisplayName='MaxRetries' />`,
      `<Field Type='Note' Name='CopilotPrompt' StaticName='CopilotPrompt' DisplayName='CopilotPrompt' NumLines='20' RichText='FALSE' Required='TRUE' />`,
      `<Field Type='Boolean' Name='AccessTeams' StaticName='AccessTeams' DisplayName='AccessTeams' />`,
      `<Field Type='Boolean' Name='AccessWeb' StaticName='AccessWeb' DisplayName='AccessWeb' />`,
      `<Field Type='Boolean' Name='AccessO365' StaticName='AccessO365' DisplayName='AccessO365' />`,
      `<Field Type='Text' Name='SubscriptionId' StaticName='SubscriptionId' DisplayName='SubscriptionId' MaxLength='255' />`,
      `<Field Type='Text' Name='SubscriptionExpiration' StaticName='SubscriptionExpiration' DisplayName='SubscriptionExpiration' MaxLength='255' />`,
      `<Field Type='Text' Name='TemplateLibraryPath' StaticName='TemplateLibraryPath' DisplayName='TemplateLibraryPath' MaxLength='1024' />`,
      `<Field Type='Text' Name='TemplateFileName' StaticName='TemplateFileName' DisplayName='TemplateFileName' MaxLength='255' />`,
      `<Field Type='Text' Name='MetadataChangeHandling' StaticName='MetadataChangeHandling' DisplayName='MetadataChangeHandling' MaxLength='50' />`,
      `<Field Type='DateTime' Name='LastUpdated' StaticName='LastUpdated' DisplayName='LastUpdated' Format='DateTime' />`
    ];

    const columnNames = [
      'SourceFolderPath',
      'DestinationFolderPath',
      'TemplateFileUrl',
      'MaxFileSizeMB',
      'ProcessingTimeoutSeconds',
      'MaxRetries',
      'CopilotPrompt',
      'AccessTeams',
      'AccessWeb',
      'AccessO365',
      'SubscriptionId',
      'SubscriptionExpiration',
      'TemplateLibraryPath',
      'TemplateFileName',
      'MetadataChangeHandling',
      'LastUpdated'
    ];

    console.log(`[addListColumns] Starting to add ${fieldsXml.length} columns to list ${listId}`);
    
    let successCount = 0;
    let skippedCount = 0;
    let errorCount = 0;
    let columnLimitReached = false;
    
    for (let i = 0; i < fieldsXml.length; i++) {
      const xml = fieldsXml[i];
      const columnName = columnNames[i];
      
      try {
        await this.createFieldXml(listId, xml);
        console.log(`[addListColumns] ✓ Column ${columnName} added successfully`);
        successCount++;
      } catch (error: any) {
        const errorMessage = error.message || String(error);
        
        // Check if column limit was reached
        if (errorMessage.includes('total size of the columns') || 
            errorMessage.includes('exceeds the limit') ||
            errorMessage.includes('column limit')) {
          if (!columnLimitReached) {
            columnLimitReached = true;
            console.warn(`[addListColumns] ⚠️ Column limit reached for list. Cannot add more columns.`);
            console.warn(`[addListColumns] Please delete unused columns from the SharePoint list to free up space.`);
          }
          skippedCount++;
          console.warn(`[addListColumns] ⚠️ Skipped column ${columnName} due to column limit`);
          continue;
        }
        
        // Column might already exist (409), that's OK
        if (errorMessage.includes('409') || 
            errorMessage.includes('already exists') || 
            errorMessage.includes('duplicate')) {
          skippedCount++;
          console.log(`[addListColumns] ℹ️ Column ${columnName} already exists, skipping`);
          continue;
        }
        
        // Other errors
        errorCount++;
        console.error(`[addListColumns] ❌ Failed to add column ${columnName}:`, errorMessage);
      }
    }
    
    // Summary logging
    if (columnLimitReached) {
      console.warn(`[addListColumns] Column creation completed with warnings.`);
      console.warn(`[addListColumns] Created: ${successCount}, Skipped: ${skippedCount}, Errors: ${errorCount}`);
      console.warn(`[addListColumns] The list has reached its column limit. Some columns may be missing.`);
    } else if (errorCount > 0) {
      console.warn(`[addListColumns] Column creation completed with some errors.`);
      console.warn(`[addListColumns] Created: ${successCount}, Skipped: ${skippedCount}, Errors: ${errorCount}`);
    } else {
      console.log(`[addListColumns] Column creation completed successfully. Created: ${successCount}, Skipped: ${skippedCount}`);
    }
    
    // Verify columns were created by checking the list fields
    console.log(`[addListColumns] Verifying columns were created...`);
    await this.verifyColumnsExist(listId, columnNames);
    
    // Small delay to ensure fields have propagated (as recommended by ChatGPT feedback)
    console.log(`[addListColumns] Waiting 500ms for field propagation...`);
    await new Promise(resolve => setTimeout(resolve, 500));
    
    // Verify again after delay
    await this.verifyColumnsExist(listId, columnNames);
    
    console.log(`[addListColumns] Finished adding columns to list`);
  }

  /**
   * Verify that columns exist in the list
   */
  private async verifyColumnsExist(listId: string, columnNames: string[]): Promise<void> {
    try {
      const url = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields?$select=InternalName,Title&$filter=Hidden eq false`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      
      if (response.ok) {
        const data = await response.json();
        const fields = data.value || data.d?.results || [];
        const fieldNames = fields.map((f: any) => f.InternalName || f.Title);
        
        console.log(`[verifyColumnsExist] Found ${fields.length} fields in list`);
        console.log(`[verifyColumnsExist] Field names:`, fieldNames);
        
        const missingColumns = columnNames.filter(name => !fieldNames.includes(name));
        if (missingColumns.length > 0) {
          console.warn(`[verifyColumnsExist] Missing columns:`, missingColumns);
        } else {
          console.log(`[verifyColumnsExist] All required columns exist`);
        }
      }
    } catch (error) {
      console.warn(`[verifyColumnsExist] Error verifying columns:`, error);
    }
  }

  /**
   * List all columns in a SharePoint list/library
   * @param listId The GUID of the list/library
   * @returns Array of column information
   */
  public async listColumns(listId: string): Promise<Array<{InternalName: string, Title: string, Type: string, ReadOnly: boolean, Required: boolean}>> {
    try {
      const url = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields?$select=InternalName,Title,TypeAsString,ReadOnlyField,Required&$filter=Hidden eq false&$orderby=Title`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      
      if (response.ok) {
        const data = await response.json();
        const fields = data.value || data.d?.results || [];
        
        return fields.map((f: any) => ({
          InternalName: f.InternalName || f.Title,
          Title: f.Title || f.InternalName,
          Type: f.TypeAsString || 'Unknown',
          ReadOnly: f.ReadOnlyField || false,
          Required: f.Required || false
        }));
      } else {
        throw new Error(`Failed to list columns: ${response.status}`);
      }
    } catch (error: any) {
      console.error(`[listColumns] Error listing columns:`, error);
      throw error;
    }
  }

  /**
   * Delete a column from a SharePoint list/library
   * @param listId The GUID of the list/library
   * @param columnInternalName The internal name of the column to delete
   * @returns true if successful
   */
  public async deleteColumn(listId: string, columnInternalName: string): Promise<boolean> {
    try {
      console.log(`[deleteColumn] Deleting column '${columnInternalName}' from list ${listId}`);
      
      // First, get the field ID
      const fieldUrl = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields/getbyinternalnameortitle('${encodeURIComponent(columnInternalName)}')?$select=Id`;
      const fieldResponse = await this.httpClient.get(fieldUrl, SPHttpClient.configurations.v1);
      
      if (!fieldResponse.ok) {
        throw new Error(`Column '${columnInternalName}' not found: ${fieldResponse.status}`);
      }
      
      const fieldData = await fieldResponse.json();
      const fieldId = fieldData.Id || fieldData.d?.Id;
      
      if (!fieldId) {
        throw new Error(`Could not get field ID for column '${columnInternalName}'`);
      }
      
      // Delete the field
      const digest = await this.getRequestDigest();
      const deleteUrl = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields(guid'${fieldId}')`;
      
      const deleteResponse = await fetch(deleteUrl, {
        method: 'DELETE',
        headers: {
          "Accept": "application/json;odata=verbose",
          "X-RequestDigest": digest,
          "If-Match": "*"
        },
        credentials: 'include'
      });
      
      if (deleteResponse.ok) {
        console.log(`[deleteColumn] ✓ Column '${columnInternalName}' deleted successfully`);
        return true;
      } else {
        const errorText = await deleteResponse.text();
        throw new Error(`Failed to delete column: ${deleteResponse.status} - ${errorText}`);
      }
    } catch (error: any) {
      console.error(`[deleteColumn] Error deleting column '${columnInternalName}':`, error);
      throw error;
    }
  }

  /**
   * List columns in a library by folder path (helper method)
   * @param sourceFolderPath The server-relative path to the folder/library
   * @returns Array of column information
   */
  public async listColumnsByPath(sourceFolderPath: string): Promise<Array<{InternalName: string, Title: string, Type: string, ReadOnly: boolean, Required: boolean}>> {
    try {
      // Get library ID using the same logic as createMetadataColumns
      let normalizedPath = sourceFolderPath;
      if (normalizedPath.startsWith(this.webUrl)) {
        normalizedPath = normalizedPath.substring(this.webUrl.length);
      }
      if (!normalizedPath.startsWith('/')) {
        normalizedPath = '/' + normalizedPath;
      }

      const parts = normalizedPath.split('/').filter(p => p && p !== '');
      let libraryName = 'Shared Documents';
      
      if (parts.length >= 3 && parts[0] === 'sites') {
        libraryName = parts[2];
      } else if (parts.length >= 1) {
        libraryName = parts[0];
      }

      const libraryUrl = `${this.webUrl}/_api/web/lists/getbytitle('${encodeURIComponent(libraryName)}')?$select=Id`;
      const libraryResponse = await this.httpClient.get(libraryUrl, SPHttpClient.configurations.v1);
      
      if (!libraryResponse.ok) {
        throw new Error(`Could not find library '${libraryName}': ${libraryResponse.status}`);
      }
      
      const libraryData = await libraryResponse.json();
      const libraryId = libraryData.d?.Id || libraryData.Id;
      
      if (!libraryId) {
        throw new Error(`Could not get library ID for '${libraryName}'`);
      }
      
      return await this.listColumns(libraryId);
    } catch (error: any) {
      console.error(`[listColumnsByPath] Error:`, error);
      throw error;
    }
  }

  /**
   * Creates a field on a SharePoint list using CreateFieldAsXml.
   * This method supports MaxLength, Boolean, Number, DateTime, Text, Note etc.
   * with explicit internal names.
   * Note: Uses fetch directly to avoid SPHttpClient header conflicts with OData V3.
   */
  private async createFieldXml(listId: string, schemaXml: string): Promise<void> {
    try {
      console.log(`[createFieldXml] Creating field with XML: ${schemaXml}`);
      
      const digest = await this.getRequestDigest();
      const url = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields/CreateFieldAsXml`;

      // CreateFieldAsXml requires odata=verbose (OData V3 format)
      const body = JSON.stringify({
        parameters: {
          "SchemaXml": schemaXml
        }
      });

      // Use fetch directly to ensure headers are not overridden by SPHttpClient
      // SPHttpClient may add odata-version headers that conflict with OData V3
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          "Accept": "application/json;odata=verbose",
          "Content-Type": "application/json;odata=verbose",
          "X-RequestDigest": digest,
          "odata-version": "3.0" // Explicitly set OData V3 version
        },
        credentials: 'include', // Include cookies for authentication
        body: body
      });

    if (!response.ok) {
      const errorText = await response.text();
      let errorJson: any = null;
      
      // Try to parse error response as JSON
      try {
        errorJson = JSON.parse(errorText);
      } catch {
        // Not JSON, use text as-is
      }
      
      // If column already exists (409), that's OK
      if (response.status === 409) {
        console.log(`[createFieldXml] Field already exists, skipping`);
        return;
      }
      
      // Handle column limit exceeded error (500)
      if (response.status === 500) {
        const errorMessage = errorJson?.error?.message?.value || 
                            errorJson?.error?.message || 
                            errorText;
        
        if (errorMessage && (
          errorMessage.includes('total size of the columns') ||
          errorMessage.includes('exceeds the limit') ||
          errorMessage.includes('column limit')
        )) {
          console.warn(`[createFieldXml] Column limit exceeded for list. Cannot add more columns.`);
          console.warn(`[createFieldXml] Error: ${errorMessage}`);
          console.warn(`[createFieldXml] Please delete some unused columns from the SharePoint list to free up space.`);
          // Don't throw - allow processing to continue without this column
          // The column might already exist or the list is at capacity
          return;
        }
      }
      
      console.error(`[createFieldXml] Failed to create field. Status:`, response.status);
      console.error(`[createFieldXml] Error response:`, errorText);
      
      // For other errors, throw but with more context
      const errorMessage = errorJson?.error?.message?.value || 
                          errorJson?.error?.message || 
                          errorText;
      throw new Error(`CreateFieldAsXml failed: ${response.status} - ${errorMessage}`);
    }

      console.log(`[createFieldXml] Field created successfully`);
    } catch (error: any) {
      console.error(`[createFieldXml] Error creating field:`, error);
      throw error;
    }
  }

  /**
   * Get field InternalName mapping (Title -> InternalName)
   */
  private async getFieldNameMapping(): Promise<Map<string, string>> {
    const mapping = new Map<string, string>();
    try {
      const listUrl = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')?$select=Id`;
      const listResponse = await this.httpClient.get(listUrl, SPHttpClient.configurations.v1);
      
      if (listResponse.ok) {
        const listData = await listResponse.json();
        const listId = listData.Id || listData.d?.Id;
        
        if (listId) {
          const fieldsUrl = `${this.webUrl}/_api/web/lists(guid'${listId}')/fields?$select=Title,InternalName&$filter=Hidden eq false`;
          const fieldsResponse = await this.httpClient.get(fieldsUrl, SPHttpClient.configurations.v1);
          
          if (fieldsResponse.ok) {
            const fieldsData = await fieldsResponse.json();
            const fields = fieldsData.value || fieldsData.d?.results || [];
            
            for (const field of fields) {
              const title = field.Title;
              const internalName = field.InternalName;
              if (title && internalName) {
                mapping.set(title, internalName);
                console.log(`[getFieldNameMapping] ${title} -> ${internalName}`);
              }
            }
          }
        }
      }
    } catch (error) {
      console.warn('[getFieldNameMapping] Error getting field mapping:', error);
    }
    
    return mapping;
  }

  /**
   * Save configuration to SMEPilotConfig list
   */
  public async saveConfiguration(config: IConfiguration): Promise<boolean> {
    try {
      // Ensure list exists
      await this.createSMEPilotConfigList();

      // Get field name mapping (Title -> InternalName)
      const fieldMapping = await this.getFieldNameMapping();
      console.log('[saveConfiguration] Field mapping obtained:', Array.from(fieldMapping.entries()));

      // Verify required fields exist
      const requiredFields = [
        'SourceFolderPath',
        'DestinationFolderPath',
        'TemplateFileUrl',
        'CopilotPrompt'
      ];
      
      const missingFields = requiredFields.filter(field => !fieldMapping.has(field));
      if (missingFields.length > 0) {
        console.error('[saveConfiguration] Missing required fields:', missingFields);
        console.error('[saveConfiguration] Available fields:', Array.from(fieldMapping.keys()));
        throw new Error(`Required fields are missing from the list. Please ensure columns are created: ${missingFields.join(', ')}`);
      }

      // Check if configuration item already exists
      const existingItem = await this.getConfigurationItem();

      // Build item body using actual InternalNames from SharePoint
      // If InternalName not found, fall back to Title (for simple names without spaces, they're usually the same)
      const getFieldName = (title: string): string => {
        const internalName = fieldMapping.get(title);
        if (!internalName) {
          console.warn(`[saveConfiguration] Field '${title}' not found in mapping, using title as fallback`);
        }
        return internalName || title;
      };

      // Helper to normalize library names (Documents -> Shared Documents)
      // SharePoint uses "Documents" as display name but "Shared Documents" as actual library name
      const normalizeLibraryName = (path: string): string => {
        if (!path) return '';
        
        // Check if path contains "Documents" (but not "Shared Documents")
        // Only replace if it's at the library level (after /sites/SiteName/)
        const parts = path.split('/').filter(p => p.trim() !== '');
        
        // If path is /sites/SiteName/Documents/..., replace "Documents" with "Shared Documents"
        if (parts.length >= 3 && parts[0] === 'sites' && parts[2] === 'Documents') {
          parts[2] = 'Shared Documents';
          return '/' + parts.join('/');
        }
        
        // If path is /Documents/..., replace "Documents" with "Shared Documents"
        if (parts.length >= 1 && parts[0] === 'Documents') {
          parts[0] = 'Shared Documents';
          return '/' + parts.join('/');
        }
        
        return path;
      };

      // Helper to normalize folder paths before saving (remove duplicates)
      const normalizePathForSave = (path: string): string => {
        if (!path) return '';
        
        let normalized = path.trim();
        
        // First, normalize library name (Documents -> Shared Documents)
        normalized = normalizeLibraryName(normalized);
        
        // Remove duplicate consecutive folder names
        const parts = normalized.split('/').filter(p => p.trim() !== '');
        const deduplicated: string[] = [];
        for (let i = 0; i < parts.length; i++) {
          if (i === 0 || parts[i] !== parts[i - 1]) {
            deduplicated.push(parts[i]);
          }
        }
        normalized = '/' + deduplicated.join('/');
        
        return normalized;
      };

      // Using odata=nometadata format - NO __metadata allowed!
      const itemBody: any = {
        [getFieldName('SourceFolderPath')]: normalizePathForSave(config.sourceFolderPath),
        [getFieldName('DestinationFolderPath')]: normalizePathForSave(config.destinationFolderPath),
        [getFieldName('TemplateFileUrl')]: normalizePathForSave(config.templateFileUrl),
        [getFieldName('MaxFileSizeMB')]: config.maxFileSizeMB,
        [getFieldName('ProcessingTimeoutSeconds')]: config.processingTimeoutSeconds,
        [getFieldName('MaxRetries')]: config.maxRetries,
        [getFieldName('CopilotPrompt')]: config.copilotPrompt,
        [getFieldName('AccessTeams')]: config.accessTeams,
        [getFieldName('AccessWeb')]: config.accessWeb,
        [getFieldName('AccessO365')]: config.accessO365,
        [getFieldName('LastUpdated')]: new Date().toISOString()
      };

      if (config.subscriptionId) {
        itemBody[getFieldName('SubscriptionId')] = config.subscriptionId;
      }

      // Get request digest once for both create and update
      const digest = await this.getRequestDigest();

      // Get the list item entity type name for __metadata (required for verbose format)
      let entityType: string | null = null;
      try {
        const listMetaUrl = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')?$select=ListItemEntityTypeFullName`;
        const metaResponse = await this.httpClient.get(listMetaUrl, SPHttpClient.configurations.v1);
        if (metaResponse.ok) {
          const metaData = await metaResponse.json();
          entityType = metaData.ListItemEntityTypeFullName || metaData.d?.ListItemEntityTypeFullName;
          console.log('[saveConfiguration] Got entity type:', entityType);
        }
      } catch (error) {
        console.warn('[saveConfiguration] Could not get entity type, will try without it:', error);
      }

      // DO NOT include __metadata - SharePoint REST API doesn't accept it for list item creation/update
      // SPHttpClient will handle the OData format automatically
      console.log('[saveConfiguration] Item body:', JSON.stringify(itemBody, null, 2));

      let response: SPHttpClientResponse;

      if (existingItem) {
        // Update existing item
        const updateUrl = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')/items(${existingItem.Id})`;
        
        console.log('[saveConfiguration] Updating item with body:', JSON.stringify(itemBody, null, 2));
        
        // Use MERGE method via SPHttpClient (more reliable than PATCH with fetch)
        response = await this.httpClient.post(
          updateUrl,
          SPHttpClient.configurations.v1,
          {
            headers: {
              'X-RequestDigest': digest,
              'IF-MATCH': '*',
              'X-HTTP-Method': 'MERGE'
            },
            body: JSON.stringify(itemBody)
          }
        );
        
        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Failed to update configuration: ${response.status} - ${errorText}`);
        }
        
        // Update successful
        console.log('Configuration updated successfully');
        return true;
      } else {
        // Create new item
        const createUrl = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')/items`;
        
        console.log('[saveConfiguration] Creating item with body:', JSON.stringify(itemBody, null, 2));
        
        // IMPORTANT: Only set X-RequestDigest header, let SPHttpClient handle Accept/Content-Type automatically
        // Manually setting Accept/Content-Type causes OData parsing errors
        response = await this.httpClient.post(
          createUrl,
          SPHttpClient.configurations.v1,
          {
            headers: {
              'X-RequestDigest': digest
            },
            body: JSON.stringify(itemBody)
          }
        );

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Failed to save configuration: ${errorText}`);
        }
      }

      console.log('Configuration saved successfully');
      return true;
    } catch (error: any) {
      console.error('Error saving configuration:', error);
      throw error;
    }
  }

  /**
   * Get configuration from SMEPilotConfig list
   */
  public async getConfiguration(): Promise<IConfiguration | null> {
    try {
      console.log('[getConfiguration] Loading configuration from SMEPilotConfig list...');
      
      // First, get the field mapping to use correct internal names
      const fieldMapping = await this.getFieldNameMapping();
      console.log('[getConfiguration] Field mapping:', Array.from(fieldMapping.entries()));
      
      const item = await this.getConfigurationItem();
      if (!item) {
        console.log('[getConfiguration] No configuration item found in list');
        return null;
      }

      console.log('[getConfiguration] Raw item data:', JSON.stringify(item, null, 2));

      // Helper to normalize folder paths (remove duplicates, normalize format)
      const normalizeFolderPath = (path: string | null | undefined): string => {
        if (!path) return '';
        
        let normalized = path.trim();
        
        // Remove duplicate consecutive folder names (e.g., "Shared Documents/Shared Documents" -> "Shared Documents")
        const parts = normalized.split('/').filter(p => p.trim() !== '');
        const deduplicated: string[] = [];
        for (let i = 0; i < parts.length; i++) {
          if (i === 0 || parts[i] !== parts[i - 1]) {
            deduplicated.push(parts[i]);
          }
        }
        normalized = '/' + deduplicated.join('/');
        
        // Ensure it starts with /sites/ if it's a full path, or keep relative path
        if (normalized.startsWith('/sites/')) {
          // Keep full path format
          return normalized;
        } else if (normalized.startsWith('/')) {
          // Relative path, keep as-is
          return normalized;
        } else {
          // Add leading slash if missing
          return '/' + normalized;
        }
      };

      // Helper to get field value using internal name or fallback to title
      // SharePoint sometimes adds numeric suffixes (e.g., SourceFolderPath0), so check both
      const getFieldValue = (title: string): any => {
        const internalName = fieldMapping.get(title);
        
        // Try multiple possible field names:
        // 1. Internal name from mapping
        // 2. Base title
        // 3. Title with "0" suffix (SharePoint quirk)
        // 4. Internal name with "0" suffix
        const possibleNames = [
          internalName,
          title,
          title + '0',
          internalName ? internalName + '0' : null
        ].filter(name => name != null);
        
        let value = null;
        for (const name of possibleNames) {
          if (item.hasOwnProperty(name) && item[name] != null) {
            value = item[name];
            console.log(`[getConfiguration] Field '${title}' found as '${name}':`, value);
            break;
          }
        }
        
        if (value == null) {
          console.warn(`[getConfiguration] Field '${title}' not found in any of these names:`, possibleNames);
          console.log(`[getConfiguration] Available item keys:`, Object.keys(item));
        }
        
        return value;
      };

      const config = {
        sourceFolderPath: normalizeFolderPath(getFieldValue('SourceFolderPath')) || '',
        destinationFolderPath: normalizeFolderPath(getFieldValue('DestinationFolderPath')) || '',
        templateFileUrl: normalizeFolderPath(getFieldValue('TemplateFileUrl')) || '',
        maxFileSizeMB: getFieldValue('MaxFileSizeMB') || 50,
        processingTimeoutSeconds: getFieldValue('ProcessingTimeoutSeconds') || 60,
        maxRetries: getFieldValue('MaxRetries') || 3,
        copilotPrompt: getFieldValue('CopilotPrompt') || '',
        accessTeams: getFieldValue('AccessTeams') !== false,
        accessWeb: getFieldValue('AccessWeb') !== false,
        accessO365: getFieldValue('AccessO365') !== false,
        subscriptionId: getFieldValue('SubscriptionId'),
        lastUpdated: getFieldValue('LastUpdated') ? new Date(getFieldValue('LastUpdated')) : undefined
      };

      console.log('[getConfiguration] Parsed configuration:', config);
      return config;
    } catch (error: any) {
      console.error('[getConfiguration] Error getting configuration:', error);
      return null;
    }
  }

  /**
   * Get configuration item from list
   */
  private async getConfigurationItem(): Promise<any | null> {
    try {
      console.log(`[getConfigurationItem] Fetching configuration from list: ${this.listName}`);
      const url = `${this.webUrl}/_api/web/lists/getbytitle('${this.listName}')/items?$top=1&$select=*`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`[getConfigurationItem] Failed to get items. Status: ${response.status}, Error: ${errorText}`);
        return null;
      }

      const data = await response.json();
      console.log(`[getConfigurationItem] Response data:`, JSON.stringify(data, null, 2));
      
      if (data.value && data.value.length > 0) {
        console.log(`[getConfigurationItem] Found ${data.value.length} configuration item(s)`);
        return data.value[0];
      }

      console.log(`[getConfigurationItem] No configuration items found in list`);
      return null;
    } catch (error: any) {
      console.error(`[getConfigurationItem] Exception getting configuration item:`, error);
      return null;
    }
  }

  /**
   * Validate configuration (check if folders/files exist)
   */
  public async validateConfiguration(config: IConfiguration): Promise<IValidationResult> {
    const errors: string[] = [];

    try {
      // Validate source folder exists
      if (!await this.folderExists(config.sourceFolderPath)) {
        errors.push(`Source folder does not exist: ${config.sourceFolderPath}`);
      }

      // Validate template file exists
      if (!await this.fileExists(config.templateFileUrl)) {
        errors.push(`Template file does not exist: ${config.templateFileUrl}`);
      } else if (!config.templateFileUrl.toLowerCase().endsWith('.dotx')) {
        errors.push(`Template file must be a .dotx file: ${config.templateFileUrl}`);
      }

      // Destination folder will be created if it doesn't exist, so no validation needed

    } catch (error: any) {
      errors.push(`Validation error: ${error.message}`);
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Check if folder exists
   */
  private async folderExists(folderPath: string): Promise<boolean> {
    try {
      // Normalize path
      let normalizedPath = folderPath;
      if (normalizedPath.startsWith(this.webUrl)) {
        normalizedPath = normalizedPath.substring(this.webUrl.length);
      }
      if (!normalizedPath.startsWith('/')) {
        normalizedPath = '/' + normalizedPath;
      }

      // Use SharePoint REST API to check folder
      // Escape single quotes instead of using encodeURIComponent to avoid double-encoding
      const safePath = normalizedPath.replace(/'/g, "''");
      const url = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${safePath}')`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      return response.ok;
    } catch {
      return false;
    }
  }

  /**
   * Check if file exists
   */
  private async fileExists(fileUrl: string): Promise<boolean> {
    try {
      // Extract server relative URL
      const serverRelativeUrl = fileUrl.replace(this.webUrl, '');
      const encodedUrl = encodeURIComponent(serverRelativeUrl);
      const url = `${this.webUrl}/_api/web/GetFileByServerRelativeUrl('${encodedUrl}')`;
      const response = await this.httpClient.get(url, SPHttpClient.configurations.v1);
      return response.ok;
    } catch {
      return false;
    }
  }

  /**
   * Create metadata columns on source folder's document library
   */
  public async createMetadataColumns(sourceFolderPath: string): Promise<boolean> {
    try {
      // Normalize the folder path (remove web URL if present)
      let normalizedPath = sourceFolderPath;
      if (normalizedPath.startsWith(this.webUrl)) {
        normalizedPath = normalizedPath.substring(this.webUrl.length);
      }
      // Ensure path starts with /
      if (!normalizedPath.startsWith('/')) {
        normalizedPath = '/' + normalizedPath;
      }

      // Get the library ID from the folder's server-relative URL
      // Note: If the folder path is wrong or the folder doesn't exist, we'll fall back to extracting library name
      // Avoid double-encoding: escape single quotes instead of using encodeURIComponent for the path
      const safePath = normalizedPath.replace(/'/g, "''");
      
      let libraryId: string | null = null;
      
      // Check if the path is likely a library root (e.g., /sites/SiteName/LibraryName)
      // Root folders don't have ListItemAllFields, so we should skip folder API calls for them
      const parts = normalizedPath.split('/').filter(p => p && p !== '');
      const isLikelyLibraryRoot = (parts.length === 3 && parts[0] === 'sites') || 
                                  (parts.length === 1 && parts[0] !== '');
      
      console.log(`[createMetadataColumns] Path analysis: normalizedPath="${normalizedPath}", parts=[${parts.join(', ')}], isLikelyLibraryRoot=${isLikelyLibraryRoot}`);
      
      // IMPORTANT: For library roots, NEVER call ListItemAllFields - it doesn't exist
      // Always skip folder API calls for library roots and go straight to library name extraction
      if (!isLikelyLibraryRoot) {
        try {
          // Method 1: Try ParentList/Id with expansion (works for subfolders and some root folders)
          const listIdUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${safePath}')?$expand=ParentList&$select=ParentList/Id`;
          const listIdResponse = await this.httpClient.get(listIdUrl, SPHttpClient.configurations.v1);
          
          if (listIdResponse.ok) {
            const listIdData = await listIdResponse.json();
            const parentList = listIdData.d?.ParentList || listIdData.ParentList;
            if (parentList && parentList.Id) {
              libraryId = parentList.Id;
              console.log(`[createMetadataColumns] Got library ID from folder ParentList (expanded): ${libraryId}`);
            }
          } else {
            // If 404 or 400, don't throw here — fall through to fallback below
            console.log(`[createMetadataColumns] Folder query returned ${listIdResponse.status}, will fallback`);
          }
          
          // If Method 1 didn't work, try direct ParentList/Id (simpler but may not work for all cases)
          if (!libraryId) {
            const directListIdUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${safePath}')/ParentList/Id`;
            const directListIdResponse = await this.httpClient.get(directListIdUrl, SPHttpClient.configurations.v1);
            
            if (directListIdResponse.ok) {
              const directListIdData = await directListIdResponse.json();
              libraryId = directListIdData.d || directListIdData;
              console.log(`[createMetadataColumns] Got library ID from folder ParentList (direct): ${libraryId}`);
            } else {
              console.log(`[createMetadataColumns] Could not get library ID from folder (${directListIdResponse.status}), will try fallback`);
            }
          }
        } catch (error) {
          console.log(`[createMetadataColumns] Folder query failed, fallback to library name:`, error);
        }
      } else {
        console.log(`[createMetadataColumns] Path appears to be a library root, skipping folder API calls`);
      }
      
      // If that didn't work, extract library name from path and get library ID by name
      // This handles cases where:
      // 1. The folder path is the library root (e.g., /sites/SiteName/ScratchDocs)
      // 2. The folder doesn't exist yet
      // 3. The folder path is wrong (e.g., missing "Shared Documents" in path)
      if (!libraryId) {
        console.log(`[createMetadataColumns] Using fallback: extracting library name from path`);
        // parts already calculated above if we're in this branch
        let libraryName = 'Shared Documents'; // default
        let serverRelativeLibraryPath = normalizedPath; // Will be adjusted below
        
        // Path format analysis:
        // /sites/SiteName/LibraryName/FolderName → parts = ['sites', 'SiteName', 'LibraryName', 'FolderName']
        // /sites/SiteName/LibraryName → parts = ['sites', 'SiteName', 'LibraryName'] (library root)
        // /LibraryName/FolderName → parts = ['LibraryName', 'FolderName']
        if (parts.length >= 3 && parts[0] === 'sites') {
          // Path includes site: /sites/SiteName/LibraryName/...
          // Library is at index 2 (after 'sites' and site name)
          libraryName = parts[2];
          // Construct library root path: /sites/SiteName/LibraryName
          serverRelativeLibraryPath = `/${parts[0]}/${parts[1]}/${parts[2]}`;
        } else if (parts.length >= 1) {
          // Path is relative: /LibraryName/...
          // Library is at index 0
          libraryName = parts[0];
          serverRelativeLibraryPath = `/${parts[0]}`;
        }
        
        console.log(`[createMetadataColumns] Extracted library name: ${libraryName}, library path: ${serverRelativeLibraryPath}`);
        
        // Method 1: Try GetList by server-relative path (more reliable than getbytitle)
        try {
          // Escape single quotes in path for REST API
          const safePath = serverRelativeLibraryPath.replace(/'/g, "''");
          const getListUrl = `${this.webUrl}/_api/web/GetList('${safePath}')?$select=Id`;
          const getListResponse = await this.httpClient.get(getListUrl, SPHttpClient.configurations.v1);
          
          if (getListResponse.ok) {
            const listData = await getListResponse.json();
            libraryId = listData.d?.Id || listData.Id;
            console.log(`[createMetadataColumns] Got library ID from GetList(serverRelativePath): ${libraryId}`);
          } else {
            console.log(`[createMetadataColumns] GetList(serverRelativePath) returned ${getListResponse.status}, trying getbytitle`);
          }
        } catch (error) {
          console.log(`[createMetadataColumns] Error with GetList(serverRelativePath), trying getbytitle:`, error);
        }
        
        // Method 2: Fallback to getbytitle if GetList failed
        if (!libraryId) {
          try {
            const libraryUrl = `${this.webUrl}/_api/web/lists/getbytitle('${encodeURIComponent(libraryName)}')?$select=Id`;
            const libraryResponse = await this.httpClient.get(libraryUrl, SPHttpClient.configurations.v1);
            
            if (libraryResponse.ok) {
              const libraryData = await libraryResponse.json();
              libraryId = libraryData.d?.Id || libraryData.Id;
              console.log(`[createMetadataColumns] Got library ID from getbytitle: ${libraryId}`);
            } else {
              console.log(`[createMetadataColumns] Could not get library ID from getbytitle (${libraryResponse.status})`);
            }
          } catch (error) {
            console.log(`[createMetadataColumns] Error getting library ID from getbytitle:`, error);
          }
        }
      }
      
      if (!libraryId) {
        throw new Error(`Could not determine library ID from folder path: ${normalizedPath}`);
      }

      return await this.addMetadataColumnsToLibrary(libraryId);
    } catch (error: any) {
      console.error('Error creating metadata columns:', error);
      throw error;
    }
  }

  /**
   * Add metadata columns to a library (helper method)
   */
  private async addMetadataColumnsToLibrary(libraryId: string): Promise<boolean> {
    try {
      // Add metadata columns (matching Function App requirements) using XML
      const metadataFieldsXml = [
        `<Field Type='Boolean' Name='SMEPilot_Enriched' StaticName='SMEPilot_Enriched' DisplayName='SMEPilot_Enriched' />`,
        `<Field Type='Text' Name='SMEPilot_Status' StaticName='SMEPilot_Status' DisplayName='SMEPilot_Status' MaxLength='50' />`,
        `<Field Type='Text' Name='SMEPilot_EnrichedFileUrl' StaticName='SMEPilot_EnrichedFileUrl' DisplayName='SMEPilot_EnrichedFileUrl' MaxLength='1024' />`,
        `<Field Type='DateTime' Name='SMEPilot_LastEnrichedTime' StaticName='SMEPilot_LastEnrichedTime' DisplayName='SMEPilot_LastEnrichedTime' Format='DateTime' />`,
        `<Field Type='Text' Name='SMEPilot_EnrichedJobId' StaticName='SMEPilot_EnrichedJobId' DisplayName='SMEPilot_EnrichedJobId' MaxLength='255' />`,
        `<Field Type='Number' Name='SMEPilot_Confidence' StaticName='SMEPilot_Confidence' DisplayName='SMEPilot_Confidence' />`,
        `<Field Type='Text' Name='SMEPilot_Classification' StaticName='SMEPilot_Classification' DisplayName='SMEPilot_Classification' MaxLength='255' />`,
        `<Field Type='Note' Name='SMEPilot_ErrorMessage' StaticName='SMEPilot_ErrorMessage' DisplayName='SMEPilot_ErrorMessage' NumLines='10' RichText='FALSE' />`,
        `<Field Type='DateTime' Name='SMEPilot_LastErrorTime' StaticName='SMEPilot_LastErrorTime' DisplayName='SMEPilot_LastErrorTime' Format='DateTime' />`
      ];

      let successCount = 0;
      let skippedCount = 0;
      let errorCount = 0;
      let columnLimitReached = false;

      for (const xml of metadataFieldsXml) {
        try {
          await this.createFieldXml(libraryId, xml);
          successCount++;
        } catch (error: any) {
          const errorMessage = error.message || String(error);
          
          // Check if column limit was reached
          if (errorMessage.includes('total size of the columns') || 
              errorMessage.includes('exceeds the limit') ||
              errorMessage.includes('column limit')) {
            if (!columnLimitReached) {
              columnLimitReached = true;
              console.warn(`[addMetadataColumnsToLibrary] Column limit reached. Some columns may not be created.`);
              console.warn(`[addMetadataColumnsToLibrary] Please delete unused columns from the SharePoint list to free up space.`);
            }
            skippedCount++;
            continue;
          }
          
          // Column might already exist (409), that's OK
          if (errorMessage.includes('409') || 
              errorMessage.includes('already exists') || 
              errorMessage.includes('duplicate')) {
            skippedCount++;
            continue;
          }
          
          // Other errors
          errorCount++;
          console.warn(`[addMetadataColumnsToLibrary] Failed to create column:`, errorMessage);
        }
      }

      if (columnLimitReached) {
        console.warn(`[addMetadataColumnsToLibrary] Column creation completed with warnings.`);
        console.warn(`[addMetadataColumnsToLibrary] Created: ${successCount}, Skipped: ${skippedCount}, Errors: ${errorCount}`);
        console.warn(`[addMetadataColumnsToLibrary] The list has reached its column limit. Some columns may be missing.`);
      } else if (errorCount > 0) {
        console.warn(`[addMetadataColumnsToLibrary] Column creation completed with some errors.`);
        console.warn(`[addMetadataColumnsToLibrary] Created: ${successCount}, Skipped: ${skippedCount}, Errors: ${errorCount}`);
      } else {
        console.log(`[addMetadataColumnsToLibrary] Metadata columns processed successfully. Created: ${successCount}, Skipped: ${skippedCount}`);
      }
      
      // Return true if at least some columns were created or already existed
      return successCount > 0 || skippedCount > 0;
    } catch (error: any) {
      console.error('Error adding metadata columns to library:', error);
      throw error;
    }
  }

  /**
   * Get drive ID from folder path using Microsoft Graph API (SPFx user context)
   * This is the most reliable method as it uses the real Graph API driveId format
   * Falls back to REST API if Graph API fails
   */
  public async getDriveIdFromFolderPath(folderPath: string): Promise<string | null> {
    try {
      console.log(`[getDriveIdFromFolderPath] Getting driveId for path: ${folderPath} using Graph API`);
      
      // Get Graph client from context
      const graphClient: MSGraphClientV3 = await this.context.msGraphClientFactory.getClient('3');
      
      // Normalize the folder path (remove web URL if present)
      let normalizedPath = folderPath;
      if (normalizedPath.startsWith(this.webUrl)) {
        normalizedPath = normalizedPath.substring(this.webUrl.length);
      }
      // Ensure path starts with /
      if (!normalizedPath.startsWith('/')) {
        normalizedPath = '/' + normalizedPath;
      }

      // Extract site path and library name from folderPath
      // Format: /sites/SiteName/LibraryName or /sites/SiteName/LibraryName/SubFolder
      const pathParts = normalizedPath.split('/').filter(p => p && p !== '');
      
      if (pathParts.length < 3 || pathParts[0] !== 'sites') {
        console.warn(`[getDriveIdFromFolderPath] Invalid path format for Graph API: ${folderPath}, falling back to REST`);
        return this.getDriveIdFromFolderPathREST(folderPath);
      }
      
      // Extract site name and library name
      const siteName = pathParts[1]; // "DocEnricher-PoC"
      const libraryName = pathParts[2]; // "Raw Docs"
      
      // Get hostname from webUrl (e.g., "onblick.sharepoint.com")
      const webUrlObj = new URL(this.webUrl);
      const hostname = webUrlObj.hostname; // "onblick.sharepoint.com"
      
      // Try method 1: Use site ID from SharePoint context (most reliable)
      let siteId: string | null = null;
      try {
        const webId = this.context.pageContext.web.id.toString();
        console.log(`[getDriveIdFromFolderPath] Trying to use site ID from context: ${webId}`);
        
        // Try to get site using the web ID directly
        const siteResponse = await graphClient
          .api(`/sites/${webId}`)
          .select('id')
          .get();
        
        if (siteResponse?.id) {
          siteId = siteResponse.id;
          console.log(`[getDriveIdFromFolderPath] ✅ Got site ID from context: ${siteId}`);
        }
      } catch (error) {
        console.log(`[getDriveIdFromFolderPath] Could not use site ID from context, trying path format`);
      }
      
      // Method 2: Use Graph API path format if method 1 failed
      if (!siteId) {
        // Graph API format: {hostname}:/sites/{siteName}
        // Example: "onblick.sharepoint.com:/sites/DocEnricher-PoC"
        const graphSitePath = `${hostname}:/sites/${siteName}`;
        
        console.log(`[getDriveIdFromFolderPath] Trying Graph site path: ${graphSitePath}, Library: ${libraryName}`);
        
        try {
          const siteResponse = await graphClient
            .api(`/sites/${graphSitePath}`)
            .select('id')
            .get();
          
          if (siteResponse?.id) {
            siteId = siteResponse.id;
            console.log(`[getDriveIdFromFolderPath] ✅ Got site ID from path: ${siteId}`);
          }
        } catch (pathError: any) {
          console.error(`[getDriveIdFromFolderPath] Graph API path method failed:`, pathError);
          // Fall through to REST fallback
        }
      }
      
      if (!siteId) {
        console.error(`[getDriveIdFromFolderPath] Could not get site ID, falling back to REST`);
        return this.getDriveIdFromFolderPathREST(folderPath);
      }
      
      console.log(`[getDriveIdFromFolderPath] Using site ID: ${siteId}`);
      
      // Get all drives for the site
      const drivesResponse = await graphClient
        .api(`/sites/${siteId}/drives`)
        .get();
      
      if (!drivesResponse?.value || drivesResponse.value.length === 0) {
        console.error(`[getDriveIdFromFolderPath] No drives found for site: ${siteId}, falling back to REST`);
        return this.getDriveIdFromFolderPathREST(folderPath);
      }
      
      // Find the drive matching the library name
      // Drive name can match library name exactly, or be normalized (spaces -> underscores, etc.)
      const normalizedLibraryName = libraryName.toLowerCase().replace(/\s+/g, '');
      const matchingDrive = drivesResponse.value.find((drive: any) => {
        const driveName = (drive.name || '').toLowerCase().replace(/\s+/g, '');
        return driveName === normalizedLibraryName || 
               drive.name === libraryName ||
               drive.name?.toLowerCase() === libraryName.toLowerCase();
      });
      
      if (!matchingDrive) {
        console.error(`[getDriveIdFromFolderPath] Drive not found for library: ${libraryName}`);
        console.log(`[getDriveIdFromFolderPath] Available drives:`, drivesResponse.value.map((d: any) => d.name));
        return this.getDriveIdFromFolderPathREST(folderPath);
      }
      
      const driveId = matchingDrive.id;
      console.log(`[getDriveIdFromFolderPath] ✅ Found driveId: ${driveId} for library: ${libraryName}`);
      return driveId;
    } catch (error: any) {
      console.error('[getDriveIdFromFolderPath] Error getting driveId from Graph API:', error);
      // Fallback to REST API method if Graph API fails
      console.log('[getDriveIdFromFolderPath] Falling back to REST API method');
      return this.getDriveIdFromFolderPathREST(folderPath);
    }
  }

  /**
   * Fallback method: Get drive ID from folder path using SharePoint REST API
   * Returns driveId in Graph API format: b! + listId (no dashes, uppercase)
   * NOTE: This format may not always work with Graph API - prefer getDriveIdFromFolderPath()
   */
  private async getDriveIdFromFolderPathREST(folderPath: string): Promise<string | null> {
    try {
      // Normalize the folder path (remove web URL if present)
      let normalizedPath = folderPath;
      if (normalizedPath.startsWith(this.webUrl)) {
        normalizedPath = normalizedPath.substring(this.webUrl.length);
      }
      // Ensure path starts with /
      if (!normalizedPath.startsWith('/')) {
        normalizedPath = '/' + normalizedPath;
      }

      // Parse the path to extract library name correctly
      const parts = normalizedPath.split('/').filter(p => p && p !== '');
      let libraryName = 'Shared Documents'; // default
      
      // Path format analysis:
      // /sites/SiteName/LibraryName/FolderName → parts = ['sites', 'SiteName', 'LibraryName', 'FolderName']
      // /LibraryName/FolderName → parts = ['LibraryName', 'FolderName']
      if (parts.length >= 3 && parts[0] === 'sites') {
        // Path includes site: /sites/SiteName/LibraryName/...
        // Library is at index 2 (after 'sites' and site name)
        libraryName = parts[2];
      } else if (parts.length >= 1) {
        // Path is relative: /LibraryName/...
        // Library is at index 0
        libraryName = parts[0];
      }
      
      // Try GetList by server-relative path first (more reliable)
      let listId: string | null = null;
      
      // Construct library root path
      let serverRelativeLibraryPath = normalizedPath;
      if (parts.length >= 3 && parts[0] === 'sites') {
        serverRelativeLibraryPath = `/${parts[0]}/${parts[1]}/${parts[2]}`;
      } else if (parts.length >= 1) {
        serverRelativeLibraryPath = `/${parts[0]}`;
      }
      
      try {
        // Method 1: Try GetList by server-relative path
        const safePath = serverRelativeLibraryPath.replace(/'/g, "''");
        const getListUrl = `${this.webUrl}/_api/web/GetList('${safePath}')?$select=Id`;
        const getListResponse = await this.httpClient.get(getListUrl, SPHttpClient.configurations.v1);
        
        if (getListResponse.ok) {
          const listData = await getListResponse.json();
          listId = listData.d?.Id || listData.Id;
          console.log(`[getDriveIdFromFolderPathREST] Got list ID from GetList: ${listId}`);
        }
      } catch (error) {
        console.warn(`[getDriveIdFromFolderPathREST] GetList failed, trying getbytitle:`, error);
      }
      
      // Method 2: Fallback to getbytitle
      if (!listId) {
        const libraryUrl = `${this.webUrl}/_api/web/lists/getbytitle('${encodeURIComponent(libraryName)}')?$select=Id`;
        const libraryResponse = await this.httpClient.get(libraryUrl, SPHttpClient.configurations.v1);
        
        if (libraryResponse.ok) {
          const libraryData = await libraryResponse.json();
          listId = libraryData.d?.Id || libraryData.Id;
          console.log(`[getDriveIdFromFolderPathREST] Got list ID from getbytitle: ${listId}`);
        } else {
          console.warn(`[getDriveIdFromFolderPathREST] Library not found: ${libraryName}`);
          return null;
        }
      }

      if (!listId) {
        return null;
      }
      
      // Convert list ID to Graph API driveId format: b! + listId without dashes, uppercase
      // NOTE: This format may not always work - Graph API may reject it
      const driveId = `b!${listId.replace(/-/g, '').toUpperCase()}`;
      console.log(`[getDriveIdFromFolderPathREST] ⚠️ Constructed driveId: ${driveId} from listId: ${listId}`);
      console.warn(`[getDriveIdFromFolderPathREST] WARNING: Using constructed driveId format. This may not work with Graph API.`);
      return driveId;
    } catch (error: any) {
      console.error('[getDriveIdFromFolderPathREST] Error getting drive ID:', error);
      return null;
    }
  }

  /**
   * Get site ID from context
   */
  public getSiteId(): string {
    // Site ID format: domain.sharepoint.com,TENANT-UUID,SITE-GUID
    const siteUrl = this.context.pageContext.site.absoluteUrl;
    const webId = this.context.pageContext.web.id.toString();
    const tenantId = this.context.pageContext.aadInfo?.tenantId?.toString() || '';
    
    // Extract domain from site URL
    try {
      const url = new URL(siteUrl);
      const domain = url.hostname;
      return `${domain},${tenantId},${webId}`;
    } catch {
      // Fallback: return web ID
      return webId;
    }
  }

  /**
   * Get all folders from document libraries (excluding system folders)
   */
  public async getFolders(): Promise<Array<{ key: string; text: string }>> {
    try {
      const folders: Array<{ key: string; text: string }> = [];
      
      // System folders to exclude (SharePoint internal folders)
      const systemFolders = ['Forms', 'FormServerTemplates', 'Item', 'Attachments', '_vti_cnf', '_vti_pvt', 'SitePages'];
      
      // Get all document libraries
      const librariesUrl = `${this.webUrl}/_api/web/lists?$filter=BaseTemplate eq 101&$select=Title,RootFolder/ServerRelativeUrl&$expand=RootFolder`;
      const response = await this.httpClient.get(librariesUrl, SPHttpClient.configurations.v1);
      
      if (!response.ok) {
        return folders;
      }

      const data = await response.json();
      const libraries = data.value || [];

      for (const library of libraries) {
        const libraryName = library.Title;
        const rootUrl = library.RootFolder?.ServerRelativeUrl || '';
        
        // Skip if this is a system library
        if (this.isSystemLibrary(libraryName)) {
          continue;
        }
        
        // Normalize path (remove web URL prefix if present)
        const normalizedRootUrl = rootUrl.replace(this.webUrl, '') || rootUrl;
        
        // Add root folder
        folders.push({
          key: normalizedRootUrl,
          text: `${libraryName} (Root)`
        });

        // Get subfolders (excluding system folders)
        try {
          const foldersUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodeURIComponent(rootUrl)}')/Folders?$select=Name,ServerRelativeUrl`;
          const foldersResponse = await this.httpClient.get(foldersUrl, SPHttpClient.configurations.v1);
          
          if (foldersResponse.ok) {
            const foldersData = await foldersResponse.json();
            const subfolders = foldersData.value || [];
            
            for (const folder of subfolders) {
              const folderName = folder.Name || '';
              
              // Skip system folders
              if (systemFolders.some(sysFolder => 
                folderName.toLowerCase() === sysFolder.toLowerCase() ||
                folder.ServerRelativeUrl.toLowerCase().includes(`/${sysFolder.toLowerCase()}/`)
              )) {
                continue;
              }
              
              // Skip folders that start with underscore or dot (system folders)
              if (folderName.startsWith('_') || folderName.startsWith('.')) {
                continue;
              }
              
              // Normalize path
              const normalizedPath = folder.ServerRelativeUrl.replace(this.webUrl, '') || folder.ServerRelativeUrl;
              folders.push({
                key: normalizedPath,
                text: `${libraryName}/${folder.Name}`
              });
            }
          }
        } catch (error) {
          console.warn(`Error fetching folders from ${libraryName}:`, error);
        }
      }

      return folders.sort((a, b) => a.text.localeCompare(b.text));
    } catch (error: any) {
      console.error('Error getting folders:', error);
      return [];
    }
  }

  /**
   * Get all .dotx template files from the site (excluding system folders)
   */
  public async getTemplateFiles(): Promise<Array<{ key: string; text: string }>> {
    try {
      const files: Array<{ key: string; text: string }> = [];
      
      // System folders to exclude (SharePoint internal folders)
      const systemFolders = ['Forms', 'FormServerTemplates', 'Item', 'Attachments', '_vti_cnf', '_vti_pvt'];
      
      // Get all document libraries
      const librariesUrl = `${this.webUrl}/_api/web/lists?$filter=BaseTemplate eq 101&$select=Title,RootFolder/ServerRelativeUrl&$expand=RootFolder`;
      const response = await this.httpClient.get(librariesUrl, SPHttpClient.configurations.v1);
      
      if (!response.ok) {
        return files;
      }

      const data = await response.json();
      const libraries = data.value || [];

      for (const library of libraries) {
        const libraryName = library.Title;
        const rootUrl = library.RootFolder?.ServerRelativeUrl || '';
        
        // Skip if this is a system library (like Site Pages, Site Assets, etc.)
        if (this.isSystemLibrary(libraryName)) {
          continue;
        }
        
        // Get all files from root folder only (not system subfolders)
        try {
          const filesUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodeURIComponent(rootUrl)}')/Files?$select=Name,ServerRelativeUrl`;
          const filesResponse = await this.httpClient.get(filesUrl, SPHttpClient.configurations.v1);
          
          if (filesResponse.ok) {
            const filesData = await filesResponse.json();
            const allFiles = filesData.value || [];
            
            // Filter for .dotx files
            const dotxFiles = allFiles.filter((file: any) => 
              file.Name && file.Name.toLowerCase().endsWith('.dotx')
            );
            
            for (const file of dotxFiles) {
              // Normalize path - preserve the actual ServerRelativeUrl as-is
              // This ensures the path matches what SharePoint uses internally
              const normalizedPath = file.ServerRelativeUrl.replace(this.webUrl, '') || file.ServerRelativeUrl;
              
              // Create display text using library Title (which might be "Documents" even if actual name is "Shared Documents")
              // But use the actual ServerRelativeUrl as the key to ensure it works with SharePoint APIs
              files.push({
                key: normalizedPath,
                text: `${libraryName}/${file.Name}`
              });
            }
          }

          // Check user-created subfolders only (exclude system folders)
          await this.getTemplateFilesFromFolder(rootUrl, libraryName, files, systemFolders);
        } catch (error) {
          console.warn(`Error fetching files from ${libraryName}:`, error);
        }
      }

      return files.sort((a, b) => a.text.localeCompare(b.text));
    } catch (error: any) {
      console.error('Error getting template files:', error);
      return [];
    }
  }

  /**
   * Check if a library is a SharePoint system library
   */
  private isSystemLibrary(libraryName: string): boolean {
    const systemLibraries = [
      'Site Pages',
      'Site Assets',
      'Style Library',
      'Master Page Gallery',
      'Theme Gallery',
      'Form Templates',
      'wfpub',
      'Workflow History',
      'Reporting Metadata',
      'Reporting Templates',
      'Pages',
      'Images',
      'Site Collection Images',
      'Site Collection Documents'
    ];
    
    return systemLibraries.some(sysLib => 
      libraryName.toLowerCase() === sysLib.toLowerCase()
    );
  }

  /**
   * Recursively get template files from a folder and its subfolders (excluding system folders)
   */
  private async getTemplateFilesFromFolder(
    folderUrl: string, 
    libraryName: string, 
    files: Array<{ key: string; text: string }>,
    systemFolders: string[]
  ): Promise<void> {
    try {
      // Get subfolders
      const foldersUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodeURIComponent(folderUrl)}')/Folders?$select=Name,ServerRelativeUrl`;
      const foldersResponse = await this.httpClient.get(foldersUrl, SPHttpClient.configurations.v1);
      
      if (foldersResponse.ok) {
        const foldersData = await foldersResponse.json();
        const subfolders = foldersData.value || [];
        
        for (const folder of subfolders) {
          const folderName = folder.Name || '';
          
          // Skip system folders
          if (systemFolders.some(sysFolder => 
            folderName.toLowerCase() === sysFolder.toLowerCase() ||
            folder.ServerRelativeUrl.toLowerCase().includes(`/${sysFolder.toLowerCase()}/`)
          )) {
            continue;
          }
          
          // Skip folders that start with underscore (system folders)
          if (folderName.startsWith('_') || folderName.startsWith('.')) {
            continue;
          }
          
          // Get files in this subfolder
          const subFilesUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodeURIComponent(folder.ServerRelativeUrl)}')/Files?$select=Name,ServerRelativeUrl`;
          const subFilesResponse = await this.httpClient.get(subFilesUrl, SPHttpClient.configurations.v1);
          
          if (subFilesResponse.ok) {
            const subFilesData = await subFilesResponse.json();
            const allSubFiles = subFilesData.value || [];
            
            // Filter for .dotx files
            const dotxFiles = allSubFiles.filter((file: any) => 
              file.Name && file.Name.toLowerCase().endsWith('.dotx')
            );
            
            for (const file of dotxFiles) {
              // Normalize path
              const normalizedPath = file.ServerRelativeUrl.replace(this.webUrl, '') || file.ServerRelativeUrl;
              // Create a cleaner display path
              const pathParts = normalizedPath.split('/');
              const fileName = pathParts[pathParts.length - 1];
              const folderPath = pathParts.slice(0, -1).join('/');
              const displayPath = folderPath ? `${folderPath}/${fileName}` : fileName;
              
              files.push({
                key: normalizedPath,
                text: displayPath.startsWith('/') ? displayPath.substring(1) : displayPath
              });
            }
          }
          
          // Recursively check nested subfolders (but skip system folders)
          await this.getTemplateFilesFromFolder(folder.ServerRelativeUrl, libraryName, files, systemFolders);
        }
      }
    } catch (error) {
      // Silently continue if folder access fails
      console.warn(`Error getting files from folder ${folderUrl}:`, error);
    }
  }

  /**
   * Upload a template file to a specified folder
   */
  public async uploadTemplateFile(file: File, targetFolderPath: string): Promise<string> {
    try {
      // Get request digest
      const digestUrl = `${this.webUrl}/_api/contextinfo`;
      const digestResponse = await this.httpClient.post(
        digestUrl, 
        SPHttpClient.configurations.v1, 
        {
          body: '' // Empty body for contextinfo
        }
      );

      if (!digestResponse.ok) {
        const errorText = await digestResponse.text();
        throw new Error(`Failed to get request digest (${digestResponse.status}): ${errorText}`);
      }

      const digestData = await digestResponse.json();
      // Handle both response formats
      const digest = digestData.d?.GetContextWebInformation?.FormDigestValue || 
                     digestData.FormDigestValue || 
                     '';
      
      if (!digest) {
        throw new Error('Request digest not found in response');
      }

      // Ensure folder exists
      const encodedPath = encodeURIComponent(targetFolderPath);
      const folderCheckUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodedPath}')`;
      const folderCheck = await this.httpClient.get(folderCheckUrl, SPHttpClient.configurations.v1);
      
      if (!folderCheck.ok) {
        // Try to create folder
        const createFolderUrl = `${this.webUrl}/_api/web/folders/add('${encodedPath}')`;
        await this.httpClient.post(createFolderUrl, SPHttpClient.configurations.v1, {
          headers: {
            'Accept': 'application/json;odata=nometadata',
            'X-RequestDigest': digest
          }
        });
      }

      // Upload file
      const fileBuffer = await file.arrayBuffer();
      const uploadUrl = `${this.webUrl}/_api/web/GetFolderByServerRelativeUrl('${encodedPath}')/Files/Add(url='${encodeURIComponent(file.name)}', overwrite=true)`;
      
      const uploadResponse = await this.httpClient.post(uploadUrl, SPHttpClient.configurations.v1, {
        body: fileBuffer,
        headers: {
          'X-RequestDigest': digest
        }
      });

      if (!uploadResponse.ok) {
        const errorText = await uploadResponse.text();
        throw new Error(`File upload failed: ${errorText}`);
      }

      const uploadData = await uploadResponse.json();
      const fileUrl = uploadData.d?.ServerRelativeUrl || `${targetFolderPath}/${file.name}`;
      
      // Return normalized path
      return fileUrl.replace(this.webUrl, '') || fileUrl;
    } catch (error: any) {
      console.error('Error uploading template file:', error);
      throw error;
    }
  }

  /**
   * Create error folders (RejectedDocs, FailedDocs) in source folder location
   */
  public async createErrorFolders(sourceFolderPath: string): Promise<boolean> {
    try {
      const folders = ['RejectedDocs', 'FailedDocs'];

      for (const folderName of folders) {
        try {
          const folderPath = `${sourceFolderPath}/${folderName}`;
          const encodedPath = encodeURIComponent(folderPath);
          const url = `${this.webUrl}/_api/web/folders/add('${encodedPath}')`;
          
          const response = await this.httpClient.post(
            url,
            SPHttpClient.configurations.v1,
            {
              headers: {
                'Accept': 'application/json;odata=nometadata',
                'Content-Type': 'application/json;odata=nometadata',
                'odata-version': ''
              }
            }
          );

          // 409 Conflict means folder already exists, which is OK
          if (!response.ok && response.status !== 409) {
            const errorText = await response.text();
            console.warn(`Failed to create folder ${folderName}: ${errorText}`);
          } else {
            console.log(`Folder ${folderName} created or already exists`);
          }
        } catch (error: any) {
          // Folder might already exist, continue
          console.warn(`Folder ${folderName} might already exist:`, error.message);
        }
      }

      return true;
    } catch (error: any) {
      console.error('Error creating error folders:', error);
      throw error;
    }
  }
}

