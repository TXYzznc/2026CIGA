import { log } from "./logger.js";
import path from "path";
import { promises as fs } from "fs";
import crypto from "crypto";

export async function retryWithBackoff(operation, operationId, maxRetries = 3, initialDelay = 5000) {
  let retries = 0;
  let delay = initialDelay;

  while (retries <= maxRetries) {
    try {
      return await operation();
    } catch (error) {
      retries++;
      if (retries > maxRetries) throw error;

      const waitTime = calculateWaitTime(error.message, delay);
      await log(
        "WARN",
        `Operation failed: ${error.message}. Retrying in ${waitTime / 1000} seconds (${retries}/${maxRetries})`
      );
      if (operationId && global.operationUpdates[operationId]) {
        global.operationUpdates[operationId].push({
          status: "WAITING",
          message: `Waiting ${waitTime / 1000} seconds before retry ${retries}/${maxRetries}`,
          retryCount: retries,
          maxRetries,
          waitTime: waitTime / 1000,
          timestamp: new Date().toISOString(),
        });
      }
      await new Promise((resolve) => setTimeout(resolve, waitTime));
      delay *= 2;
    }
  }
}

function calculateWaitTime(errorMessage, defaultDelay) {
  const gpuQuotaMatch = errorMessage?.match(
    /exceeded your GPU quota.*(?:retry|wait)\s*(?:in|after)?\s*(?:(\d+):(\d+):(\d+)|(\d+)\s*(?:seconds|s)|(\d+)\s*(?:minutes|m)|(\d+)\s*(?:hours|h))/i
  );
  if (!gpuQuotaMatch) return defaultDelay;

  const [_, h, m, s, sec, min, hr] = gpuQuotaMatch;
  return (
    (parseInt(h || 0) * 3600 + parseInt(m || 0) * 60 + parseInt(s || 0)) * 1000 ||
    parseInt(sec || 0) * 1000 ||
    parseInt(min || 0) * 60 * 1000 ||
    parseInt(hr || 0) * 3600 * 1000 ||
    60 * 1000
  );
}

export function sanitizePrompt(prompt) {
  if (!prompt || typeof prompt !== 'string') {
    return '';
  }
  
  // Enhanced sanitization:
  // 1. Trim whitespace
  // 2. Remove potentially harmful characters (keeping alphanumeric, spaces, and basic punctuation)
  // 3. Limit length to 500 characters
  return prompt.trim()
    .replace(/[^\w\s.,!?-]/g, '')
    .slice(0, 500);
}

export function generateUniqueFilename(prefix, ext, toolName) {
  const timestamp = Date.now();
  const uniqueId = crypto.randomBytes(4).toString("hex");
  return `${prefix}_${toolName}_${timestamp}_${uniqueId}.${ext}`;
}

// Parse resource URI templates
export function parseResourceUri(uri) {
  // Support for templated URIs like asset://{type}/{id}
  const match = uri.match(/^asset:\/\/(?:([^\/]+)\/)?(.+)$/);
  if (!match) return null;
  
  const [, type, id] = match;
  return { type, id };
}

// Helper to detect image format from buffer
export function detectImageFormat(buffer) {
  if (!buffer || buffer.length < 4) {
    return "Unknown";
  }
  
  // Check for JPEG
  if (buffer[0] === 0xFF && buffer[1] === 0xD8 && buffer[2] === 0xFF) {
    return "JPEG";
  }
  
  // Check for PNG
  if (buffer[0] === 0x89 && buffer[1] === 0x50 && buffer[2] === 0x4E && buffer[3] === 0x47) {
    return "PNG";
  }
  
  // Default to PNG if unknown
  return "PNG";
}

// Helper to save files from URL
export async function saveFileFromUrl(url, prefix, ext, toolName, assetsDir, hfToken, workDir) {
  if (!url || typeof url !== 'string' || !url.startsWith("http")) {
    throw new Error("Invalid URL provided");
  }

  const filename = generateUniqueFilename(prefix, ext, toolName);
  const filePath = path.join(assetsDir, filename);
  
  // Security check: ensure file path is within assetsDir
  if (!filePath.startsWith(assetsDir)) {
    throw new Error("Invalid file path - security violation");
  }

  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`HTTP error! Status: ${response.status}`);
    }
    const buffer = await response.arrayBuffer();
    await fs.writeFile(filePath, Buffer.from(buffer));
    
    // Return both the file path and the resource URI
    return {
      filePath,
      resourceUri: `asset://${filename}`
    };
  } catch (error) {
    await log('ERROR', `Error saving file from URL: ${error.message}`, workDir);
    throw new Error("Failed to save file from URL");
  }
}

export async function saveFileFromData(data, prefix, ext, toolName, assetsDir, hfToken = null, modelSpace = null, workDir = null) {
  // Validate required parameters
  if (!data) {
    throw new Error("No data provided to save");
  }
  
  if (!prefix || typeof prefix !== "string") {
    throw new Error("prefix must be a defined string");
  }
  
  if (!ext || typeof ext !== "string") {
    throw new Error("ext must be a defined string");
  }
  
  if (!toolName || typeof toolName !== "string") {
    throw new Error("toolName must be a defined string");
  }
  
  if (!assetsDir || typeof assetsDir !== "string") {
    throw new Error("assetsDir must be a defined string");
  }

  // Log parameters for debugging if workDir is available
  if (workDir) {
    await log('DEBUG', `saveFileFromData called with: prefix=${prefix}, ext=${ext}, toolName=${toolName}, assetsDir=${assetsDir}`, workDir);
  }

  const filename = generateUniqueFilename(prefix, ext, toolName);
  const filePath = path.join(assetsDir, filename);
  
  // Security check: ensure file path is within assetsDir
  if (!filePath.startsWith(assetsDir)) {
    throw new Error("Invalid file path - security violation");
  }

  try {
    // Handle different data types
    if (data instanceof Blob || data instanceof File) {
      if (workDir) await log('DEBUG', "Saving data as Blob/File", workDir);
      const arrayBuffer = await data.arrayBuffer();
      await fs.writeFile(filePath, Buffer.from(arrayBuffer));
    } else if (typeof data === 'string') {
      // Check if it's base64 encoded
      if (data.match(/^data:[^;]+;base64,/)) {
        if (workDir) await log('DEBUG', "Saving data as base64 string", workDir);
        const base64Data = data.split(',')[1];
        await fs.writeFile(filePath, Buffer.from(base64Data, 'base64'));
      } else {
        if (workDir) await log('DEBUG', "Saving data as regular string", workDir);
        await fs.writeFile(filePath, data);
      }
    } else if (data instanceof ArrayBuffer || ArrayBuffer.isView(data)) {
      if (workDir) await log('DEBUG', "Saving data as ArrayBuffer", workDir);
      await fs.writeFile(filePath, Buffer.from(data));
    } else if (Array.isArray(data) && data.length > 0) {
      // Handle array of file data (common in InstantMesh API responses)
      if (workDir) await log('DEBUG', "Data is an array with " + data.length + " items", workDir);
      const fileData = data[0];
      
      if (fileData.url) {
        if (workDir) await log('DEBUG', "Found URL in data: " + fileData.url, workDir);
        // Fetch the file from the URL with authentication
        const headers = hfToken ? { Authorization: `Bearer ${hfToken}` } : {};
        if (workDir && hfToken) await log('DEBUG', "Adding HF token authentication to URL fetch request", workDir);
        
        // Verify the URL domain matches the expected domain for the configured space
        if (modelSpace) {
          try {
            const urlDomain = new URL(fileData.url).hostname;
            const expectedDomain = modelSpace.split('/')[0].toLowerCase() + '-' + modelSpace.split('/')[1].toLowerCase() + '.hf.space';
            
            if (urlDomain !== expectedDomain && workDir) {
              await log('WARN', `URL domain mismatch: Expected "${expectedDomain}" but got "${urlDomain}". This suggests your MODEL_SPACE setting doesn't match the space that generated this URL.`, workDir);
            }
          } catch (urlError) {
            if (workDir) await log('WARN', `Error parsing URL: ${urlError.message}`, workDir);
          }
        }
        
        const response = await fetch(fileData.url, { headers });
        if (!response.ok) {
          throw new Error(`Failed to fetch file: ${response.status} ${response.statusText}`);
        }
        
        const buffer = await response.arrayBuffer();
        await fs.writeFile(filePath, Buffer.from(buffer));
        if (workDir) await log('DEBUG', "Successfully saved file from URL", workDir);
      } else {
        if (workDir) await log('DEBUG', "No URL found in array data, saving as JSON", workDir);
        await fs.writeFile(filePath, JSON.stringify(data));
      }
    } else if (typeof data === 'object' && data.url) {
      // Handle object with URL property
      if (workDir) await log('DEBUG', "Data is an object with URL: " + data.url, workDir);
      // Fetch the file from the URL with authentication
      const headers = hfToken ? { Authorization: `Bearer ${hfToken}` } : {};
      if (workDir && hfToken) await log('DEBUG', "Adding HF token authentication to URL fetch request", workDir);
      
      // Verify the URL domain matches the expected domain for the configured space
      if (modelSpace) {
        try {
          const urlDomain = new URL(data.url).hostname;
          const expectedDomain = modelSpace.split('/')[0].toLowerCase() + '-' + modelSpace.split('/')[1].toLowerCase() + '.hf.space';
          
          if (urlDomain !== expectedDomain && workDir) {
            await log('WARN', `URL domain mismatch: Expected "${expectedDomain}" but got "${urlDomain}". This suggests your MODEL_SPACE setting doesn't match the space that generated this URL.`, workDir);
          }
        } catch (urlError) {
          if (workDir) await log('WARN', `Error parsing URL: ${urlError.message}`, workDir);
        }
      }
      
      const response = await fetch(data.url, { headers });
      if (!response.ok) {
        throw new Error(`Failed to fetch file: ${response.status} ${response.statusText}`);
      }
      
      const buffer = await response.arrayBuffer();
      await fs.writeFile(filePath, Buffer.from(buffer));
      if (workDir) await log('DEBUG', "Successfully saved file from URL", workDir);
    } else if (typeof data === 'object') {
      // JSON or other object
      if (workDir) await log('DEBUG', "Saving data as JSON object", workDir);
      await fs.writeFile(filePath, JSON.stringify(data));
    } else {
      // Fallback
      if (workDir) await log('DEBUG', "Saving data using fallback method", workDir);
      await fs.writeFile(filePath, Buffer.from(String(data)));
    }
    
    // Return both the file path and the resource URI
    return {
      filePath,
      resourceUri: `asset://${filename}`
    };
  } catch (error) {
    if (workDir) {
      // Provide more detailed error messages for common issues
      if (error.message.includes("404") || error.message.includes("Not Found")) {
        await log('ERROR', `Error saving file from data: ${error.message}. This may be due to an incorrect model space configuration. Please check your MODEL_SPACE environment variable.`, workDir);
      } else if (error.message.includes("401") || error.message.includes("unauthorized")) {
        await log('ERROR', `Error saving file from data: ${error.message}. This may be due to authentication issues. Please check your HF_TOKEN environment variable.`, workDir);
      } else {
        await log('ERROR', `Error saving file from data: ${error.message}`, workDir);
      }
    }
    
    throw new Error("Failed to save file from data");
  }
}

export function getMimeType(filename) {
  if (filename.endsWith(".png")) return "image/png";
  if (filename.endsWith(".jpg") || filename.endsWith(".jpeg")) return "image/jpeg";
  if (filename.endsWith(".obj")) return "model/obj";
  if (filename.endsWith(".glb")) return "model/gltf-binary";
  return "application/octet-stream"; // Default
}

// Simple rate limiting
const rateLimits = new Map(); // Internal module state for rate limiting

export function checkRateLimit(clientId, limit = 10, windowMs = 60000) {
  const now = Date.now();
  const clientKey = clientId || 'default';
  
  if (!rateLimits.has(clientKey)) {
    rateLimits.set(clientKey, { count: 1, resetAt: now + windowMs });
    return true;
  }
  
  const clientLimit = rateLimits.get(clientKey);
  if (now > clientLimit.resetAt) {
    clientLimit.count = 1;
    clientLimit.resetAt = now + windowMs;
    return true;
  }
  
  if (clientLimit.count >= limit) {
    return false;
  }
  
  clientLimit.count++;
  return true;
}