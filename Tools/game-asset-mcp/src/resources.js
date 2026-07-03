import { ListResourcesRequestSchema, ReadResourceRequestSchema, ListResourceTemplatesRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { promises as fs } from "fs";
import path from "path";
import { log } from "./logger.js";
import { getMimeType, parseResourceUri } from "./utils.js";

export function registerResourceHandlers(server, config) {
  const { assetsDir, workDir } = config;

  server.setRequestHandler(ListResourcesRequestSchema, async (request) => {
    await log("INFO", "Listing resources", workDir);
    
    try {
      // Check if there's a filter in the request
      const uriTemplate = request.params?.uriTemplate;
      let typeFilter = null;
      
      if (uriTemplate) {
        const templateMatch = uriTemplate.match(/^asset:\/\/([^\/]+)\/.*$/);
        if (templateMatch) {
          typeFilter = templateMatch[1];
          await log('INFO', `Filtering resources by type: ${typeFilter}`, workDir);
        }
      }
      
      const files = await fs.readdir(assetsDir, { withFileTypes: true });
      const resources = await Promise.all(
        files
          .filter(f => f.isFile())
          .map(async (file) => {
            const filePath = path.join(assetsDir, file.name);
            const stats = await fs.stat(filePath);
            const filenameParts = file.name.split('_');
            const assetType = filenameParts[0] || 'unknown';
            const toolOrigin = filenameParts[1] || 'unknown';
            
            // Create a structured URI that includes the type
            const uri = `asset://${assetType}/${file.name}`;
            
            return {
              uri,
              name: file.name,
              mimetype: getMimeType(file.name),
              created: stats.ctime.toISOString(),
              size: stats.size,
              toolOrigin,
              assetType
            };
          })
      );
      
      // Apply type filter if specified
      const filteredResources = typeFilter
        ? resources.filter(r => r.assetType === typeFilter)
        : resources;
      
      return { resources: filteredResources };
    } catch (error) {
      await log('ERROR', `Error listing resources: ${error.message}`, workDir);
      return { resources: [] };
    }
  });

  server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
    const uri = request.params.uri;
    await log("INFO", `Reading resource: ${uri}`, workDir);
    
    if (uri.startsWith("asset://")) {
      // Parse the URI to handle templated URIs
      const parsedUri = parseResourceUri(uri);
      
      if (!parsedUri) {
        throw new Error("Invalid resource URI format");
      }
      
      // For templated URIs like asset://{type}/{id}, the filename is in the id part
      // For traditional URIs like asset://filename, the id is the filename
      const filename = parsedUri.type && parsedUri.id.includes('/')
        ? parsedUri.id
        : (parsedUri.type ? `${parsedUri.type}/${parsedUri.id}` : parsedUri.id);
      
      // Remove any type prefix if it exists
      const actualFilename = filename.includes('/') ? filename.split('/').pop() : filename;
      const filePath = path.join(assetsDir, actualFilename);
      
      // Security check: ensure file path is within assetsDir
      if (!filePath.startsWith(assetsDir)) {
        throw new Error("Invalid resource path - security violation");
      }
      
      try {
        const stats = await fs.stat(filePath);
        if (!stats.isFile()) {
          throw new Error("Not a file");
        }
        
        const data = await fs.readFile(filePath);
        const mimetype = getMimeType(actualFilename);
        
        return {
          contents: [{
            uri: uri,
            mimeType: mimetype,
            blob: data.toString("base64") // Binary data as base64
          }]
        };
      } catch (error) {
        await log('ERROR', `Error reading resource: ${error.message}`, workDir);
        return {
          content: [{ type: "text", text: "Error reading resource" }],
          isError: true
        };
      }
    }
    
    throw new Error("Unsupported URI scheme");
  });
  
  // Resource templates handler
  server.setRequestHandler(ListResourceTemplatesRequestSchema, async () => {
    return {
      templates: [
        {
          uriTemplate: "asset://{type}/{id}",
          name: "Generated Asset",
          description: "Filter assets by type and ID"
        }
      ]
    };
  });
}