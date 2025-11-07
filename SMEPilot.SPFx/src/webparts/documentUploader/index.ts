// A file is required to be in the root of the ./src directory named index.ts
// SPFx loads this module and expects the web part class as the default export

export * from './DocumentUploaderWebPart';
import DocumentUploaderWebPart from './DocumentUploaderWebPart';

// Ensure the default export is the web part class
export default DocumentUploaderWebPart;


