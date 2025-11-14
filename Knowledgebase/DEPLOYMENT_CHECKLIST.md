# SMEPilot Production Deployment Checklist

## Pre-Deployment Validation

### Environment Readiness
- [ ] SharePoint Online site is available
- [ ] Azure Function App is deployed and running
- [ ] Azure AD App Registration is created
- [ ] Application Insights is configured
- [ ] All required permissions are granted

### Code Readiness
- [ ] All code is committed to source control
- [ ] SPFx solution is built (`gulp bundle --ship`)
- [ ] SPFx package is created (`gulp package-solution --ship`)
- [ ] Function App is deployed to Azure
- [ ] All environment variables are configured

### Configuration Readiness
- [ ] Template file is uploaded to Templates library
- [ ] Source folder is created and accessible
- [ ] Destination folder path is determined
- [ ] Function App URL is documented
- [ ] Copilot Agent prompt is finalized

## Deployment Steps

### Phase 1: Azure Setup
- [ ] Deploy Function App to Azure
- [ ] Configure Application Settings
- [ ] Enable Application Insights
- [ ] Configure CORS settings
- [ ] Test Function App health endpoint
- [ ] Verify webhook endpoint is accessible

### Phase 2: Azure AD Configuration
- [ ] Create App Registration
- [ ] Configure API permissions
- [ ] Grant admin consent
- [ ] Create client secret
- [ ] Document app details (Client ID, Tenant ID, Secret)

### Phase 3: SPFx Deployment
- [ ] Upload `.sppkg` to App Catalog
- [ ] Approve API permission requests
- [ ] Add web part to SharePoint page
- [ ] Configure web part (Function App URL)
- [ ] Verify web part loads correctly

### Phase 4: Initial Configuration
- [ ] Fill configuration form
- [ ] Save configuration
- [ ] Verify SMEPilotConfig list is created
- [ ] Verify metadata columns are created
- [ ] Verify error folders are created
- [ ] Verify webhook subscription is created
- [ ] Verify subscription ID is stored

### Phase 5: Copilot Agent Setup (REQUIRED)
- [ ] Access Copilot Studio
- [ ] Create Copilot Agent
- [ ] Configure data source (SMEPilot Enriched Docs library)
- [ ] Configure system prompt
- [ ] Configure access points
- [ ] Deploy Copilot Agent
- [ ] Verify agent is accessible

## Post-Deployment Verification

### Function App Verification
- [ ] Function App is running
- [ ] Application Insights is logging
- [ ] Health endpoint returns OK
- [ ] Webhook endpoint is accessible
- [ ] No errors in Application Insights

### SharePoint Verification
- [ ] SMEPilotConfig list exists and has data
- [ ] Metadata columns exist in source library
- [ ] Error folders exist (RejectedDocs, FailedDocs)
- [ ] Web part is accessible
- [ ] Configuration can be viewed/edited

### Document Processing Verification
- [ ] Upload test document
- [ ] Verify webhook triggers Function App
- [ ] Verify document is processed
- [ ] Verify enriched document is saved
- [ ] Verify metadata is updated
- [ ] Verify source document remains

### Copilot Agent Verification
- [ ] Agent is accessible in Teams
- [ ] Agent is accessible in Web (if configured)
- [ ] Agent is accessible in O365 (if configured)
- [ ] Agent responds to queries
- [ ] Citations include document links
- [ ] Security trimming works

## Performance Validation

### Processing Performance
- [ ] Small file (< 1MB) processes in < 30 seconds
- [ ] Medium file (1-10MB) processes in < 60 seconds
- [ ] Large file (10-50MB) processes in < 120 seconds
- [ ] Multiple files (10 files) all process successfully
- [ ] Concurrent uploads (5 files) all process safely

### System Performance
- [ ] Function App response time < 2 seconds
- [ ] SharePoint page load time < 3 seconds
- [ ] Copilot Agent response time < 5 seconds
- [ ] No memory leaks during batch processing
- [ ] Application Insights shows no errors

## Security Validation

### Permissions
- [ ] App Registration has minimal required permissions
- [ ] Users have appropriate folder permissions
- [ ] Security trimming works in Copilot Agent
- [ ] No unauthorized access to Function App
- [ ] Client secret is stored securely

### Data Protection
- [ ] Source documents are preserved
- [ ] Enriched documents are stored securely
- [ ] Metadata is protected
- [ ] Error messages don't expose sensitive data
- [ ] Logs don't contain sensitive information

## Monitoring Setup

### Application Insights
- [ ] Custom metrics are configured
- [ ] Alerts are set up for errors
- [ ] Alerts are set up for processing failures
- [ ] Dashboard is created
- [ ] Log retention is configured

### SharePoint Monitoring
- [ ] ULS logging is enabled
- [ ] Search indexing is monitored
- [ ] Library usage is tracked
- [ ] Error folder contents are reviewed

## Rollback Plan

### If Deployment Fails
1. [ ] Document the failure point
2. [ ] Check Application Insights for errors
3. [ ] Review SharePoint ULS logs
4. [ ] Rollback SPFx solution (if needed)
5. [ ] Rollback Function App (if needed)
6. [ ] Restore previous configuration (if needed)

### Rollback Steps
- [ ] Remove web part from pages
- [ ] Remove `.sppkg` from App Catalog
- [ ] Revert Function App to previous version
- [ ] Restore previous configuration from backup
- [ ] Notify users of rollback

## Documentation

### Required Documentation
- [ ] Installation guide is complete
- [ ] User guide is complete
- [ ] Troubleshooting guide is available
- [ ] API documentation is available
- [ ] Configuration reference is available

### Training Materials
- [ ] Admin training guide
- [ ] User training guide
- [ ] Video tutorials (if applicable)
- [ ] FAQ document
- [ ] Support contact information

## Sign-Off

### Technical Sign-Off
- [ ] All tests passed
- [ ] Performance meets requirements
- [ ] Security validation passed
- [ ] Monitoring is configured
- [ ] Documentation is complete

### Business Sign-Off
- [ ] Requirements are met
- [ ] User acceptance testing passed
- [ ] Training is complete
- [ ] Support plan is in place
- [ ] Go-live date is confirmed

## Post-Go-Live

### Week 1
- [ ] Monitor Application Insights daily
- [ ] Review error folder contents
- [ ] Check webhook subscription status
- [ ] Verify Copilot Agent is working
- [ ] Collect user feedback

### Week 2-4
- [ ] Review processing statistics
- [ ] Optimize performance if needed
- [ ] Address user issues
- [ ] Update documentation based on feedback
- [ ] Plan for enhancements

---

**Deployment Date**: _______________  
**Deployed By**: _______________  
**Verified By**: _______________  
**Sign-Off**: _______________

