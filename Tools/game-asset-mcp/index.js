#!/usr/bin/env node

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { SSEServerTransport } from "@modelcontextprotocol/sdk/server/sse.js";
import {
  ListToolsRequestSchema,
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
  ListPromptsRequestSchema,
  GetPromptRequestSchema,
  ListResourceTemplatesRequestSchema
} from "@modelcontextprotocol/sdk/types.js";
import { Client } from "@gradio/client";
import { InferenceClient } from "@huggingface/inference";
import { promises as fs } from "fs";
import path from "path";
import express from "express";
import crypto from "crypto";
import dotenv from "dotenv";
import { z } from "zod";
import https from "https";

// Load environment variables from .env file
console.error("========== ENVIRONMENT VARIABLE DEBUGGING ==========");
console.error(`Current working directory: ${process.cwd()}`);

// Check if .env file exists
try {
  const envPath = path.join(process.cwd(), '.env');
  const envExamplePath = path.join(process.cwd(), '.env.example');
  
  console.error(`Checking if .env file exists at: ${envPath}`);
  const envExists = fs.existsSync(envPath);
  console.error(`.env file exists: ${envExists}`);
  
  if (envExists) {
    console.error(`Reading contents of .env file:`);
    const envContents = fs.readFileSync(envPath, 'utf8');
    console.error(`----- .env file contents -----`);
    console.error(envContents);
    console.error(`-----------------------------`);
  }
  
  console.error(`Checking if .env.example file exists at: ${envExamplePath}`);
  const envExampleExists = fs.existsSync(envExamplePath);
  console.error(`.env.example file exists: ${envExampleExists}`);
} catch (error) {
  console.error(`Error checking .env files: ${error.message}`);
}

// Force load from specific path and override existing environment variables
console.error(`Loading .env file explicitly from path with override...`);
const dotenvResult = dotenv.config({
  path: path.join(process.cwd(), '.env'),
  override: true // This makes .env variables override system environment variables
});
if (dotenvResult.error) {
  console.error(`Error loading .env file: ${dotenvResult.error.message}`);
} else {
  console.error(`.env file loaded successfully from: ${dotenvResult.parsed ? Object.keys(dotenvResult.parsed).length + " variables" : "unknown"}`);
  if (dotenvResult.parsed) {
    console.error(`Parsed variables: ${JSON.stringify(dotenvResult.parsed)}`);
  }
}

// Force reload the MODEL_SPACE variable from .env to ensure it overrides system variables
if (dotenvResult.parsed && dotenvResult.parsed.MODEL_SPACE) {
  console.error(`Forcing MODEL_SPACE to value from .env: "${dotenvResult.parsed.MODEL_SPACE}"`);
  process.env.MODEL_SPACE = dotenvResult.parsed.MODEL_SPACE;
}

// Debug log for environment variables
console.error(`ENV: MODEL_SPACE = "${process.env.MODEL_SPACE}"`);
console.error(`ENV: HF_TOKEN = "${process.env.HF_TOKEN ? "***" + process.env.HF_TOKEN.substring(process.env.HF_TOKEN.length - 4) : "not set"}"`);
console.error(`ENV: MODEL_3D_STEPS = "${process.env.MODEL_3D_STEPS || "not set"}"`);
console.error(`ENV: MODEL_3D_GUIDANCE_SCALE = "${process.env.MODEL_3D_GUIDANCE_SCALE || "not set"}"`);
console.error(`ENV: MODEL_3D_OCTREE_RESOLUTION = "${process.env.MODEL_3D_OCTREE_RESOLUTION || "not set"}"`);
console.error(`ENV: MODEL_3D_SEED = "${process.env.MODEL_3D_SEED || "not set"}"`);
console.error(`ENV: MODEL_3D_REMOVE_BACKGROUND = "${process.env.MODEL_3D_REMOVE_BACKGROUND || "not set"}"`);
console.error(`ENV: MODEL_3D_TURBO_MODE = "${process.env.MODEL_3D_TURBO_MODE || "not set"}"`);

// Verify MODEL_SPACE is set correctly
console.error(`VERIFICATION: MODEL_SPACE should be set to the value from .env file`);
console.error(`VERIFICATION: Expected value from .env: "mubarak-alketbi/Hunyuan3D-2mini-Turbo"`);
console.error(`VERIFICATION: Actual value in process.env: "${process.env.MODEL_SPACE}"`);
console.error(`VERIFICATION: Is correct? ${process.env.MODEL_SPACE === "mubarak-alketbi/Hunyuan3D-2mini-Turbo"}`);
console.error("====================================================");

// Allow working directory to be specified via command-line argument
const workDir = process.argv[2] || process.cwd();

// Logging function with file output
async function log(level = 'INFO', message) {
  // If only one parameter is provided, assume it's the message
  if (!message) {
    message = level;
    level = 'INFO';
  }
  
  const timestamp = new Date().toISOString();
  const logMessage = `[${level.toUpperCase()}] ${timestamp} - ${message}\n`;
  
  // Log to console
  console.error(logMessage.trim());
  
  // Log to file
  try {
    const logDir = path.join(workDir, 'logs');
    await fs.mkdir(logDir, { recursive: true });
    const logFile = path.join(logDir, 'server.log');
    await fs.appendFile(logFile, logMessage);
  } catch (err) {
    console.error(`Failed to write to log file: ${err}`);
  }
}

// Enhanced logging with operation ID for tracking long-running operations
let operationCounter = 0;
// Global object to store operation updates that can be accessed by clients
global.operationUpdates = {};

// Function to notify clients that the resource list has changed
async function notifyResourceListChanged() {
  await log('DEBUG', "Notifying clients of resource list change");
  await server.notification({ method: "notifications/resources/list_changed" });
  
  // For SSE transport, notify all connected clients
  if (global.transports && global.transports.size > 0) {
    for (const [clientId, transport] of global.transports) {
      try {
        await transport.sendNotification({ method: "notifications/resources/list_changed" });
        await log('DEBUG', `Sent resource list change notification to client ${clientId}`);
      } catch (error) {
        await log('ERROR', `Failed to send notification to client ${clientId}: ${error.message}`);
      }
    }
  }
}

async function logOperation(toolName, operationId, status, details = {}) {
  const level = status === 'ERROR' ? 'ERROR' : 'INFO';
  const detailsStr = Object.entries(details)
    .map(([key, value]) => `${key}: ${value}`)
    .join(', ');
  
  const logMessage = `Operation ${operationId} [${toolName}] - ${status}${detailsStr ? ' - ' + detailsStr : ''}`;
  await log(level, logMessage);
  
  // Store the update in the global object for potential client access
  if (!global.operationUpdates[operationId]) {
    global.operationUpdates[operationId] = [];
  }
  
  global.operationUpdates[operationId].push({
    status: status,
    details: details,
    timestamp: new Date().toISOString(),
    message: logMessage
  });
  
  // Limit the size of the updates array to prevent memory issues
  if (global.operationUpdates[operationId].length > 100) {
    global.operationUpdates[operationId].shift(); // Remove the oldest update
  }
}

// Retry function with exponential backoff
async function retryWithBackoff(operation, operationId = null, maxRetries = 3, initialDelay = 5000) {
  let retries = 0;
  let delay = initialDelay;
  
  while (true) {
    try {
      return await operation();
    } catch (error) {
      retries++;
      
      // Check if we've exceeded the maximum number of retries
      if (retries > maxRetries) {
        throw error;
      }
      
      // Check if the error is due to GPU quota with improved regex to handle multiple time formats
      const gpuQuotaMatch = error.message?.match(/exceeded your GPU quota.*(?:retry|wait)\s*(?:in|after)?\s*(?:(\d+):(\d+):(\d+)|(\d+)\s*(?:seconds|s)|(\d+)\s*(?:minutes|m)|(\d+)\s*(?:hours|h))/i);
      if (gpuQuotaMatch) {
        let waitTime;
        let waitTimeSource = "unknown format";
        
        if (gpuQuotaMatch[1]) { // HH:MM:SS format
          const hours = parseInt(gpuQuotaMatch[1]) || 0;
          const minutes = parseInt(gpuQuotaMatch[2]) || 0;
          const seconds = parseInt(gpuQuotaMatch[3]) || 0;
          waitTime = (hours * 3600 + minutes * 60 + seconds) * 1000;
          waitTimeSource = `${hours}h:${minutes}m:${seconds}s format`;
        } else if (gpuQuotaMatch[4]) { // Seconds format
          waitTime = parseInt(gpuQuotaMatch[4]) * 1000;
          waitTimeSource = `${gpuQuotaMatch[4]} seconds format`;
        } else if (gpuQuotaMatch[5]) { // Minutes format
          waitTime = parseInt(gpuQuotaMatch[5]) * 60 * 1000;
          waitTimeSource = `${gpuQuotaMatch[5]} minutes format`;
        } else if (gpuQuotaMatch[6]) { // Hours format
          waitTime = parseInt(gpuQuotaMatch[6]) * 3600 * 1000;
          waitTimeSource = `${gpuQuotaMatch[6]} hours format`;
        }
        
        // If no valid time was parsed, use a safe default
        if (!waitTime || waitTime <= 0) {
          waitTime = 60 * 1000; // Default to 60 seconds
          waitTimeSource = "default fallback";
        }
        
        const waitTimeSeconds = Math.ceil(waitTime/1000);
        const waitMessage = `GPU quota exceeded. Waiting for ${waitTimeSeconds} seconds before retry ${retries}/${maxRetries} (detected from ${waitTimeSource})`;
        await log('WARN', waitMessage);
        
        // If this is part of a 3D asset generation operation, update the client
        // Only if operationId is provided and the updates object exists
        if (operationId && global.operationUpdates && global.operationUpdates[operationId]) {
          global.operationUpdates[operationId].push({
            status: "WAITING",
            message: waitMessage,
            retryCount: retries,
            maxRetries: maxRetries,
            waitTime: waitTimeSeconds,
            timestamp: new Date().toISOString()
          });
        }
        
        await new Promise(resolve => setTimeout(resolve, waitTime + 1000)); // Add 1 second buffer
      } else if (error.message?.includes("GPU quota") || error.message?.includes("quota exceeded")) {
        // GPU quota error detected but format not recognized
        const defaultDelay = 60 * 1000; // 60 seconds as a safe default
        await log('WARN', `GPU quota error with unrecognized format: "${error.message}". Using default wait time of ${defaultDelay/1000} seconds before retry ${retries}/${maxRetries}`);
        
        // Update operation status if available
        if (operationId && global.operationUpdates && global.operationUpdates[operationId]) {
          global.operationUpdates[operationId].push({
            status: "WAITING",
            message: `GPU quota exceeded. Using default wait time of ${defaultDelay/1000} seconds before retry ${retries}/${maxRetries}`,
            retryCount: retries,
            maxRetries: maxRetries,
            waitTime: defaultDelay/1000,
            timestamp: new Date().toISOString()
          });
        }
        
        await new Promise(resolve => setTimeout(resolve, defaultDelay));
      } else {
        // For other errors, use exponential backoff
        await log('WARN', `Operation failed: ${error.message}. Retrying in ${delay/1000} seconds (${retries}/${maxRetries})`);
        await new Promise(resolve => setTimeout(resolve, delay));
        delay *= 2; // Exponential backoff
      }
    }
  }
}

// Define MCP Error Codes
const MCP_ERROR_CODES = {
  InvalidRequest: -32600,
  MethodNotFound: -32601,
  InvalidParams: -32602,
  InternalError: -32603,
  ParseError: -32700
};

// Initialize MCP server
const server = new Server(
  { name: "game-asset-generator", version: "0.3.0" }, // Updated to version 0.3.0 with Hunyuan3D-2mini-Turbo support
  {
    capabilities: {
      tools: { list: true, call: true },
      resources: { list: true, read: true, listChanged: true }, // Added listChanged capability
      prompts: { list: true, get: true }
    }
  }
);
// Create working directory if it doesn't exist
await fs.mkdir(workDir, { recursive: true });

// Create a dedicated assets directory
const assetsDir = path.join(workDir, "assets");
await fs.mkdir(assetsDir, { recursive: true });

// Simple rate limiting
const rateLimits = new Map();
function checkRateLimit(clientId, limit = 10, windowMs = 60000) {
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
await fs.mkdir(workDir, { recursive: true });

// Define Zod schemas for input validation
const schema2D = z.object({
  prompt: z.string().min(1).max(500).transform(val => sanitizePrompt(val))
});

const schema3D = z.object({
  prompt: z.string().min(1).max(500).transform(val => sanitizePrompt(val))
});

// Tool definitions
const TOOLS = {
  GENERATE_2D_ASSET: {
    name: "generate_2d_asset",
    description: "Generate a 2D game asset (e.g., pixel art sprite) from a text prompt.",
    inputSchema: {
      type: "object",
      properties: {
        prompt: { type: "string", description: "Text description of the 2D asset (e.g., 'pixel art sword')" }
      },
      required: ["prompt"]
    }
  },
  GENERATE_3D_ASSET: {
    name: "generate_3d_asset",
    description: "Generate a 3D game asset (e.g., OBJ model) from a text prompt.",
    inputSchema: {
      type: "object",
      properties: {
        prompt: { type: "string", description: "Text description of the 3D asset (e.g., 'isometric 3D castle')" }
      },
      required: ["prompt"]
    }
  }
};
// Get environment variables
const hfToken = process.env.HF_TOKEN;

// Get MODEL_SPACE from environment variable with fallback
const modelSpaceFromEnv = process.env.MODEL_SPACE;
const modelSpace = modelSpaceFromEnv || "mubarak-alketbi/InstantMesh";

// Debug log for modelSpace
console.error("========== MODEL SPACE DEBUGGING ==========");
console.error(`MODEL_SPACE from environment: "${modelSpaceFromEnv}"`);
console.error(`MODEL_SPACE after fallback: "${modelSpace}"`);
console.error(`Is MODEL_SPACE using default fallback? ${!modelSpaceFromEnv}`);
console.error(`Does MODEL_SPACE include "hunyuan"? ${modelSpace.toLowerCase().includes("hunyuan")}`);
console.error(`Does MODEL_SPACE include "hunyuan3d-2mini"? ${modelSpace.toLowerCase().includes("hunyuan3d-2mini")}`);
console.error(`Does MODEL_SPACE include "instantmesh"? ${modelSpace.toLowerCase().includes("instantmesh")}`);
console.error("===========================================");

// Get and validate 3D model configuration parameters from environment variables with defaults
// Function to validate numeric value within a range
function validateNumericRange(value, min, max, defaultValue, paramName) {
  if (value === null || value === undefined) {
    return defaultValue;
  }
  
  const numValue = Number(value);
  if (isNaN(numValue)) {
    console.error(`Invalid ${paramName} value: "${value}" is not a number. Using default: ${defaultValue}`);
    return defaultValue;
  }
  
  if (numValue < min) {
    console.error(`${paramName} value ${numValue} is below minimum (${min}). Using minimum value.`);
    return min;
  }
  
  if (numValue > max) {
    console.error(`${paramName} value ${numValue} exceeds maximum (${max}). Using maximum value.`);
    return max;
  }
  
  return numValue;
}

// Function to validate enum values
function validateEnum(value, allowedValues, defaultValue, paramName) {
  if (value === null || value === undefined) {
    return defaultValue;
  }
  
  if (!allowedValues.includes(value)) {
    console.error(`Invalid ${paramName} value: "${value}". Allowed values: [${allowedValues.join(', ')}]. Using default: ${defaultValue}`);
    return defaultValue;
  }
  
  return value;
}

// Parse and validate steps (will be validated per model type later)
const model3dSteps = process.env.MODEL_3D_STEPS ? parseInt(process.env.MODEL_3D_STEPS) : null;

// Parse and validate guidance scale (0.0-100.0)
const model3dGuidanceScale = process.env.MODEL_3D_GUIDANCE_SCALE ?
  validateNumericRange(parseFloat(process.env.MODEL_3D_GUIDANCE_SCALE), 0.0, 100.0, null, "MODEL_3D_GUIDANCE_SCALE") : null;

// Parse octree resolution (will be validated per model type later)
const model3dOctreeResolution = process.env.MODEL_3D_OCTREE_RESOLUTION || null;

// Parse and validate seed (0-10000000)
const model3dSeed = process.env.MODEL_3D_SEED ?
  validateNumericRange(parseInt(process.env.MODEL_3D_SEED), 0, 10000000, null, "MODEL_3D_SEED") : null;

// Parse and validate remove background (boolean)
const model3dRemoveBackground = process.env.MODEL_3D_REMOVE_BACKGROUND ?
  process.env.MODEL_3D_REMOVE_BACKGROUND.toLowerCase() === 'true' : true; // Default to true if not specified

// Parse and validate turbo mode (enum: "Turbo", "Fast", "Standard")
const validTurboModes = ["Turbo", "Fast", "Standard"];
const model3dTurboMode = validateEnum(
  process.env.MODEL_3D_TURBO_MODE,
  validTurboModes,
  "Turbo",
  "MODEL_3D_TURBO_MODE"
);

// Space types
const SPACE_TYPE = {
  INSTANTMESH: "instantmesh",
  HUNYUAN3D: "hunyuan3d",
  HUNYUAN3D_MINI_TURBO: "hunyuan3d_mini_turbo",
  UNKNOWN: "unknown"
};

// Space detection state
let detectedSpaceType = SPACE_TYPE.UNKNOWN;
// Validate space format
function validateSpaceFormat(space) {
  // Check if the space follows the format "username/space-name"
  if (!space) {
    return false;
  }
  
  // Check basic format with regex
  const spaceRegex = /^[a-zA-Z0-9_-]+\/[a-zA-Z0-9_-]+$/;
  if (!spaceRegex.test(space)) {
    return false;
  }
  
  // Additional validation
  const parts = space.split('/');
  if (parts.length !== 2) {
    return false;
  }
  
  const [username, spaceName] = parts;
  // Username and space name should be at least 2 characters
  if (username.length < 2 || spaceName.length < 2) {
    return false;
  }
  
  return true;
}

// Detect which space was duplicated by checking available endpoints using view_api()
async function detectSpaceType(client) {
  try {
    await log('INFO', "Detecting space type using view_api()...");
    await log('DEBUG', "========== SPACE DETECTION DEBUGGING ==========");
    await log('DEBUG', `Current modelSpace: "${modelSpace}"`);
    await log('DEBUG', `modelSpace lowercase: "${modelSpace.toLowerCase()}"`);
    await log('DEBUG', `Contains "instantmesh": ${modelSpace.toLowerCase().includes("instantmesh")}`);
    await log('DEBUG', `Contains "hunyuan3d-2mini-turbo": ${modelSpace.toLowerCase().includes("hunyuan3d-2mini-turbo")}`);
    await log('DEBUG', `Contains "hunyuan3d-2mini": ${modelSpace.toLowerCase().includes("hunyuan3d-2mini")}`);
    await log('DEBUG', `Contains "hunyuan3dmini": ${modelSpace.toLowerCase().includes("hunyuan3dmini")}`);
    await log('DEBUG', `Contains "hunyuan": ${modelSpace.toLowerCase().includes("hunyuan")}`);
    await log('DEBUG', "==============================================");
    
    // First, check if the space name contains a hint about the type
    // Check for Hunyuan3D-2mini-Turbo first (most specific match)
    if (modelSpace.toLowerCase().includes("hunyuan3d-2mini-turbo") ||
        modelSpace.toLowerCase().includes("hunyuan3d-2mini") ||
        modelSpace.toLowerCase().includes("hunyuan3dmini")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      await log('INFO', `Detected space type: Hunyuan3D-2mini-Turbo (based on space name)`);
      await log('DEBUG', `Space detection result: HUNYUAN3D_MINI_TURBO (${SPACE_TYPE.HUNYUAN3D_MINI_TURBO})`);
      return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
    }
    // Then check for regular Hunyuan3D-2
    else if (modelSpace.toLowerCase().includes("hunyuan")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
      await log('INFO', `Detected space type: Hunyuan3D-2 (based on space name)`);
      await log('DEBUG', `Space detection result: HUNYUAN3D (${SPACE_TYPE.HUNYUAN3D})`);
      return SPACE_TYPE.HUNYUAN3D;
    }
    // Finally check for InstantMesh
    else if (modelSpace.toLowerCase().includes("instantmesh")) {
      detectedSpaceType = SPACE_TYPE.INSTANTMESH;
      await log('INFO', `Detected space type: InstantMesh (based on space name)`);
      await log('DEBUG', `Space detection result: INSTANTMESH (${SPACE_TYPE.INSTANTMESH})`);
      return SPACE_TYPE.INSTANTMESH;
    }
    
    await log('DEBUG', "No space type detected from name, continuing with API endpoint detection...");
    
    // Try a direct predict call to test if the client is working
    try {
      await log('DEBUG', "Testing client with a simple predict call...");
      // Try a simple predict call with an empty API name
      const result = await client.predict("", []);
      await log('DEBUG', `Predict call result: ${JSON.stringify(result)}`);
    } catch (predictError) {
      await log('DEBUG', `Simple predict call error: ${predictError.message}`);
      // This is expected to fail, but it helps test if the client is working
    }
    
    // Add a timeout to the view_api call
    await log('DEBUG', "Creating view_api promise...");
    const apiInfoPromise = client.view_api(true); // true to show all endpoints
    
    // Create a timeout promise
    const timeoutPromise = new Promise((_, reject) => {
      setTimeout(() => {
        reject(new Error("view_api call timed out after 30 seconds"));
      }, 30000); // 30 second timeout (increased from 20 seconds)
    });
    
    // Race the API info promise against the timeout
    await log('DEBUG', "Starting view_api call with 30 second timeout...");
    const apiInfo = await Promise.race([apiInfoPromise, timeoutPromise]);
    
    // Log the full API info for debugging
    await log('DEBUG', `API info retrieved: ${JSON.stringify(apiInfo, null, 2)}`);
    
    // Check for InstantMesh-specific endpoints in named_endpoints
    if (apiInfo && apiInfo.named_endpoints) {
      const endpoints = Object.keys(apiInfo.named_endpoints);
      await log('DEBUG', `Available endpoints: ${endpoints.join(', ')}`);
      
      // Check for InstantMesh-specific endpoints
      if (endpoints.includes("/check_input_image") ||
          endpoints.includes("/make3d") ||
          endpoints.includes("/generate_mvs") ||
          endpoints.includes("/preprocess")) {
        detectedSpaceType = SPACE_TYPE.INSTANTMESH;
        await log('INFO', `Detected space type: InstantMesh (based on API endpoints)`);
        return SPACE_TYPE.INSTANTMESH;
      }
      
      // Check for Hunyuan3D-2mini-Turbo-specific endpoints
      if (endpoints.includes("/on_gen_mode_change") ||
          endpoints.includes("/on_decode_mode_change") ||
          endpoints.includes("/on_export_click")) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
        await log('INFO', `Detected space type: Hunyuan3D-2mini-Turbo (based on API endpoints)`);
        return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      }
      
      // Check for Hunyuan3D-specific endpoints
      if (endpoints.includes("/shape_generation") ||
          endpoints.includes("/generation_all")) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
        await log('INFO', `Detected space type: Hunyuan3D-2 (based on API endpoints)`);
        return SPACE_TYPE.HUNYUAN3D;
      }
    }
    
    // If we get here, we couldn't determine the space type from named_endpoints
    // Check unnamed_endpoints as well
    if (apiInfo && apiInfo.unnamed_endpoints) {
      const unnamedEndpoints = Object.keys(apiInfo.unnamed_endpoints);
      await log('DEBUG', `Available unnamed endpoints: ${unnamedEndpoints.join(', ')}`);
      
      // Check for InstantMesh-specific endpoints in unnamed_endpoints
      if (unnamedEndpoints.some(endpoint =>
          endpoint.includes("check_input_image") ||
          endpoint.includes("make3d") ||
          endpoint.includes("generate_mvs") ||
          endpoint.includes("preprocess"))) {
        detectedSpaceType = SPACE_TYPE.INSTANTMESH;
        await log('INFO', `Detected space type: InstantMesh (based on unnamed API endpoints)`);
        return SPACE_TYPE.INSTANTMESH;
      }
      
      // Check for Hunyuan3D-2mini-Turbo-specific endpoints in unnamed_endpoints
      if (unnamedEndpoints.some(endpoint =>
          endpoint.includes("on_gen_mode_change") ||
          endpoint.includes("on_decode_mode_change") ||
          endpoint.includes("on_export_click"))) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
        await log('INFO', `Detected space type: Hunyuan3D-2mini-Turbo (based on unnamed API endpoints)`);
        return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      }
      
      // Check for Hunyuan3D-specific endpoints in unnamed_endpoints
      if (unnamedEndpoints.some(endpoint =>
          endpoint.includes("shape_generation") ||
          endpoint.includes("generation_all"))) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
        await log('INFO', `Detected space type: Hunyuan3D-2 (based on unnamed API endpoints)`);
        return SPACE_TYPE.HUNYUAN3D;
      }
    }
    
    // If we still can't determine the space type, check the space name as a hint
    await log('DEBUG', "Fallback space detection from name...");
    // Check for Hunyuan3D-2mini-Turbo first (most specific match)
    if (modelSpace.toLowerCase().includes("hunyuan3d-2mini-turbo") ||
        modelSpace.toLowerCase().includes("hunyuan3d-2mini") ||
        modelSpace.toLowerCase().includes("hunyuan3dmini")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      await log('INFO', `Detected space type: Hunyuan3D-2mini-Turbo (based on space name fallback)`);
      await log('DEBUG', `Fallback space detection result: HUNYUAN3D_MINI_TURBO (${SPACE_TYPE.HUNYUAN3D_MINI_TURBO})`);
      return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
    }
    // Then check for regular Hunyuan3D-2
    else if (modelSpace.toLowerCase().includes("hunyuan")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
      await log('INFO', `Detected space type: Hunyuan3D-2 (based on space name fallback)`);
      await log('DEBUG', `Fallback space detection result: HUNYUAN3D (${SPACE_TYPE.HUNYUAN3D})`);
      return SPACE_TYPE.HUNYUAN3D;
    }
    // Finally check for InstantMesh
    else if (modelSpace.toLowerCase().includes("instantmesh")) {
      detectedSpaceType = SPACE_TYPE.INSTANTMESH;
      await log('INFO', `Detected space type: InstantMesh (based on space name fallback)`);
      await log('DEBUG', `Fallback space detection result: INSTANTMESH (${SPACE_TYPE.INSTANTMESH})`);
      return SPACE_TYPE.INSTANTMESH;
    }
    // If we get here, we couldn't determine the space type
    // This is a critical error - we should not proceed without knowing the space type
    const errorMessage = `Could not determine space type after API analysis. Please ensure your MODEL_SPACE environment variable in .env file is set correctly according to .env.example. You must use one of the following options:
1. A Hunyuan3D-2 space (containing "hunyuan" in the name)
2. A Hunyuan3D-2mini-Turbo space (containing "hunyuan3d-2mini" in the name)
3. An InstantMesh space (containing "instantmesh" in the name)`;
    
    await log('ERROR', errorMessage);
    throw new Error(errorMessage);
  } catch (error) {
    await log('ERROR', `Error detecting space type: ${error.message}`);
    // Rethrow the error instead of defaulting to InstantMesh
    throw new Error(`Failed to detect space type: ${error.message}. Please check your MODEL_SPACE environment variable in .env file and ensure it follows the format specified in .env.example.`);
  }
}
// Authentication options for Gradio using HF token
const authOptions = { hf_token: hfToken };

// Connect to Hugging Face Spaces and Inference API
let modelClient;
let inferenceClient;

// Initialize Hugging Face Inference Client for 2D and 3D asset generation
if (!hfToken) {
  await log('ERROR', "HF_TOKEN is required in the .env file for 2D and 3D asset generation");
  throw new Error("HF_TOKEN is required in the .env file for 2D and 3D asset generation");
}

try {
  await log('INFO', "Initializing Hugging Face Inference Client...");
  await log('DEBUG', `HF_TOKEN length: ${hfToken ? hfToken.length : 0}`);
  await log('DEBUG', `HF_TOKEN first 4 chars: ${hfToken ? hfToken.substring(0, 4) : 'none'}`);
  
  inferenceClient = new InferenceClient(hfToken);
  await log('INFO', "Successfully initialized Hugging Face Inference Client");
  await log('DEBUG', "InferenceClient initialized successfully");
} catch (error) {
  await log('ERROR', `Error initializing Hugging Face Inference Client: ${error.message}`);
  await log('DEBUG', `Error stack: ${error.stack}`);
  throw new Error("Failed to initialize Hugging Face Inference Client. Check your HF_TOKEN.");
}

// Connect to Model Space API using Gradio client
try {
  // Validate model space format
  if (!validateSpaceFormat(modelSpace)) {
    await log('ERROR', `Invalid model space format: "${modelSpace}". Format must be "username/space-name"`);
    throw new Error(`Invalid model space format: "${modelSpace}". Format must be "username/space-name" (e.g., "your-username/InstantMesh" or "your-username/Hunyuan3D-2"). Please check your MODEL_SPACE environment variable in the .env file. You need to:
1. Duplicate either space from:
   - https://huggingface.co/spaces/mubarak-alketbi/InstantMesh
   - https://huggingface.co/spaces/mubarak-alketbi/Hunyuan3D-2
2. Set MODEL_SPACE to your username and space name (e.g., "your-username/InstantMesh")
3. Make sure your HF_TOKEN has access to this space`);
  }
  
  await log('INFO', `Connecting to model space: ${modelSpace}...`);
  await log('INFO', "Using HF token authentication");
  
  // Additional logging for debugging
  await log('DEBUG', `MODEL_SPACE environment variable: "${modelSpaceFromEnv}"`);
  await log('DEBUG', `MODEL_SPACE after default fallback: "${modelSpace}"`);
  await log('DEBUG', `Is MODEL_SPACE using default? ${!modelSpaceFromEnv}`);
  
  // Check if the space exists before trying to connect to it
  await log('DEBUG', "Checking if space exists...");
  let spaceExists = false;
  let alternativeSpace = null;
  
  try {
    // Try to fetch the space URL to see if it exists
    const spaceUrl = `https://huggingface.co/spaces/${modelSpace}`;
    await log('DEBUG', `Checking space URL: ${spaceUrl}`);
    
    const response = await fetch(spaceUrl, {
      method: 'HEAD',
      headers: { Authorization: `Bearer ${hfToken}` }
    });
    
    spaceExists = response.ok;
    await log('DEBUG', `Space exists check result: ${spaceExists} (status: ${response.status})`);
    
    if (!spaceExists) {
      // If the space doesn't exist, try alternative casings
      if (modelSpace.toLowerCase().includes("hunyuan3d-2mini") ||
          modelSpace.toLowerCase().includes("hunyuan3dmini")) {
        // Try different casings for Hunyuan3D-2mini-Turbo
        const alternatives = [
          `${modelSpace.split('/')[0]}/Hunyuan3D-2mini-Turbo`,
          `${modelSpace.split('/')[0]}/hunyuan3d-2mini-turbo`,
          `${modelSpace.split('/')[0]}/Hunyuan3D-2mini`,
          `${modelSpace.split('/')[0]}/hunyuan3d-2mini`
        ];
        
        for (const alt of alternatives) {
          const altUrl = `https://huggingface.co/spaces/${alt}`;
          await log('DEBUG', `Checking alternative space URL: ${altUrl}`);
          
          const altResponse = await fetch(altUrl, {
            method: 'HEAD',
            headers: { Authorization: `Bearer ${hfToken}` }
          });
          
          if (altResponse.ok) {
            alternativeSpace = alt;
            await log('INFO', `Found alternative space: ${alternativeSpace}`);
            break;
          }
        }
      } else if (modelSpace.toLowerCase().includes("hunyuan")) {
        // Try different casings for Hunyuan3D-2
        const alternatives = [
          `${modelSpace.split('/')[0]}/Hunyuan3D-2`,
          `${modelSpace.split('/')[0]}/hunyuan3d-2`,
          `${modelSpace.split('/')[0]}/HunyuanD-2`
        ];
        
        for (const alt of alternatives) {
          const altUrl = `https://huggingface.co/spaces/${alt}`;
          await log('DEBUG', `Checking alternative space URL: ${altUrl}`);
          
          const altResponse = await fetch(altUrl, {
            method: 'HEAD',
            headers: { Authorization: `Bearer ${hfToken}` }
          });
          
          if (altResponse.ok) {
            alternativeSpace = alt;
            await log('INFO', `Found alternative space: ${alternativeSpace}`);
            break;
          }
        }
      } else if (modelSpace.toLowerCase().includes("instantmesh")) {
        // Try different casings for InstantMesh
        const alternatives = [
          `${modelSpace.split('/')[0]}/InstantMesh`,
          `${modelSpace.split('/')[0]}/instantmesh`,
          `${modelSpace.split('/')[0]}/Instantmesh`
        ];
        
        for (const alt of alternatives) {
          const altUrl = `https://huggingface.co/spaces/${alt}`;
          await log('DEBUG', `Checking alternative space URL: ${altUrl}`);
          
          const altResponse = await fetch(altUrl, {
            method: 'HEAD',
            headers: { Authorization: `Bearer ${hfToken}` }
          });
          
          if (altResponse.ok) {
            alternativeSpace = alt;
            await log('INFO', `Found alternative space: ${alternativeSpace}`);
            break;
          }
        }
      }
    }
  } catch (error) {
    await log('WARN', `Error checking if space exists: ${error.message}`);
    // Continue anyway, as the space might still be accessible
  }
  
  // Use the alternative space if found
  if (alternativeSpace) {
    await log('INFO', `Using alternative space: ${alternativeSpace} instead of ${modelSpace}`);
    await log('DEBUG', `Changing MODEL_SPACE from "${modelSpace}" to "${alternativeSpace}"`);
    // Store the original value for debugging
    const originalModelSpace = modelSpace;
    modelSpace = alternativeSpace;
    await log('DEBUG', `MODEL_SPACE changed from "${originalModelSpace}" to "${modelSpace}"`);
  }
  
  // Add a timeout to the connection attempt
  await log('DEBUG', `Creating connection promise for ${modelSpace} with token length ${hfToken.length}`);
  const connectionPromise = Client.connect(modelSpace, authOptions);
  
  // Create a timeout promise
  const timeoutPromise = new Promise((_, reject) => {
    setTimeout(() => {
      reject(new Error(`Connection to ${modelSpace} timed out after 60 seconds`));
    }, 60000); // 60 second timeout (increased from 30 seconds)
  });
  
  try {
    // Race the connection promise against the timeout
    await log('DEBUG', "Starting connection attempt with 60 second timeout...");
    modelClient = await Promise.race([connectionPromise, timeoutPromise]);
    await log('INFO', `Successfully connected to model space: ${modelSpace}`);
    
    // Add more diagnostic logs
    await log('DEBUG', "Connection successful, checking client object...");
    await log('DEBUG', `Client object type: ${typeof modelClient}`);
    await log('DEBUG', `Client object methods: ${Object.getOwnPropertyNames(Object.getPrototypeOf(modelClient)).join(', ')}`);
    
    // Detect which space was duplicated
    await log('DEBUG', `Starting space type detection for "${modelSpace}"...`);
    // Log the modelSpace value right before detection
    await log('DEBUG', `About to detect space type for: "${modelSpace}"`);
    await log('DEBUG', `modelSpace lowercase: "${modelSpace.toLowerCase()}"`);
    await log('DEBUG', `Contains "hunyuan3d-2mini-turbo": ${modelSpace.toLowerCase().includes("hunyuan3d-2mini-turbo")}`);
    await log('DEBUG', `Contains "hunyuan": ${modelSpace.toLowerCase().includes("hunyuan")}`);
    await log('DEBUG', `Contains "instantmesh": ${modelSpace.toLowerCase().includes("instantmesh")}`);
    
    const spaceType = await detectSpaceType(modelClient);
    // We successfully connected to the space, so it's valid
    // Even if we couldn't determine the exact type, we'll use the detected type
    await log('INFO', `Using space type: ${spaceType}`);
    await log('DEBUG', `Final detected space type: ${spaceType}`);
    
  } catch (error) {
    await log('ERROR', `Error connecting to model space: ${error.message}`);
    await log('DEBUG', `Error stack: ${error.stack}`);
    
    if (error.message.includes("timed out")) {
      await log('ERROR', `Connection to model space "${modelSpace}" timed out. This could be due to network issues or the space being unavailable.`);
      throw new Error(`Connection to model space "${modelSpace}" timed out. Please check your internet connection and try again later. If the problem persists, the space might be unavailable or overloaded.`);
    } else if (error.message.includes("not found") || error.message.includes("404")) {
      await log('ERROR', `Model space "${modelSpace}" not found. Please make sure you've duplicated either InstantMesh or Hunyuan3D-2 space and set the correct space name in your .env file.`);
      throw new Error(`Model space "${modelSpace}" not found. This could be because: 1. The space doesn't exist - verify you've duplicated it correctly, 2. You've entered the wrong format - it should be "username/space-name" not the full URL, 3. The space is private and your token doesn't have access to it. Please duplicate either space and set the correct name in your .env file.`);
    } else if (error.message.includes("unauthorized") || error.message.includes("401")) {
      await log('ERROR', `Unauthorized access to model space "${modelSpace}". Please check your HF_TOKEN and make sure it has access to this space.`);
      throw new Error(`Unauthorized access to model space "${modelSpace}". This means your HF_TOKEN doesn't have permission to access this space. Please: 1. Make sure your HF_TOKEN is correct and not expired, 2. Ensure the space is either public or you have granted access to your account, 3. Try generating a new token with appropriate permissions at https://huggingface.co/settings/tokens`);
    } else {
      throw error;
    }
  }
} catch (error) {
  await log('ERROR', `Error connecting to model space: ${error.message}`);
  throw new Error(`Failed to connect to model space: ${error.message}`);
}

// Register tool list handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [TOOLS.GENERATE_2D_ASSET, TOOLS.GENERATE_3D_ASSET]
  };
});

// Tool call handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const toolName = request.params.name;
  const args = request.params.arguments;

  await log('INFO', `Calling tool: ${toolName}`);

  try {
    if (toolName === TOOLS.GENERATE_2D_ASSET.name) {
      const { prompt } = schema2D.parse(args);
      if (!prompt) {
        throw new Error("Invalid or empty prompt");
      }
      await log('INFO', `Generating 2D asset with prompt: "${prompt}"`);
      
      // Use the Hugging Face Inference API to generate the image
      await log('DEBUG', "Calling Hugging Face Inference API for 2D asset generation...");
      // Enhance the prompt to specify high detail, complete object, and white background
      const enhancedPrompt = `${prompt}, high detailed, complete object, not cut off, white solid background`;
      await log('DEBUG', `Enhanced 2D prompt: "${enhancedPrompt}"`);
      
      const image = await inferenceClient.textToImage({
        model: "gokaygokay/Flux-2D-Game-Assets-LoRA",
        inputs: enhancedPrompt,
        parameters: { num_inference_steps: 50 },
        provider: "hf-inference",
      });
      
      if (!image) {
        throw new Error("No image returned from 2D asset generation API");
      }
      
      // Save the image (which is a Blob) and notify clients of resource change
      // Detect the actual image format (JPEG or PNG)
      const imageBuffer = await image.arrayBuffer();
      const format = detectImageFormat(Buffer.from(imageBuffer));
      const extension = format === "JPEG" ? "jpg" : "png";
      
      await log('DEBUG', `Detected 2D image format: ${format}, using extension: ${extension}`);
      const saveResult = await saveFileFromData(image, "2d_asset", extension, toolName);
      await log('INFO', `2D asset saved at: ${saveResult.filePath}`);
      
      // Notify clients that a new resource is available
      await notifyResourceListChanged();
      
      return {
        content: [{ type: "text", text: `2D asset available at ${saveResult.resourceUri}` }],
        isError: false
      };
    }

    if (toolName === TOOLS.GENERATE_3D_ASSET.name) {
      const operationId = `3D-${++operationCounter}`;
      await logOperation(toolName, operationId, 'STARTED');
      
      try {
        const { prompt } = schema3D.parse(args);
        if (!prompt) {
          throw new Error("Invalid or empty prompt");
        }
        await log('INFO', `Generating 3D asset with prompt: "${prompt}"`);
        await logOperation(toolName, operationId, 'PROCESSING', { step: 'Parsing prompt', prompt });
        
        // Initial response to prevent timeout with more detailed information
        const initialResponse = {
          content: [
            {
              type: "text",
              text: `Starting 3D asset generation (Operation ID: ${operationId})...\n\n` +
                    `This process involves several steps:\n` +
                    `1. Generating initial 3D image from prompt\n` +
                    `2. Validating image for 3D conversion\n` +
                    `3. Preprocessing image (removing background)\n` +
                    `4. Generating multi-view images\n` +
                    `5. Creating 3D models (OBJ and GLB)\n\n` +
                    `This may take several minutes. The process will continue in the background.\n` +
                    `You'll see status updates here for any significant events (like GPU quota limits).\n` +
                    `The final 3D models will be available when the process completes.`
            }
          ],
          isError: false,
          metadata: {
            operationId: operationId,
            status: "STARTED",
            startTime: new Date().toISOString(),
            prompt: prompt
          }
        };
        
        // Start the 3D asset generation process in the background
        (async () => {
          try {
            // Step 1: Generate the initial image using the Inference API
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Generating initial image' });
            await log('DEBUG', "Calling Hugging Face Inference API for 3D asset generation...");
            
            // Use retry mechanism for the image generation
            // Enhance the prompt to specify high detail, complete object, and white background
            const enhancedPrompt = `${prompt}, high detailed, complete object, not cut off, white solid background`;
            await log('DEBUG', `Enhanced 3D prompt: "${enhancedPrompt}"`);
            
            const image = await retryWithBackoff(async () => {
              return await inferenceClient.textToImage({
                model: "gokaygokay/Flux-Game-Assets-LoRA-v2",
                inputs: enhancedPrompt,
                parameters: { num_inference_steps: 50 },
                provider: "hf-inference",
              });
            }, operationId);
            
            if (!image) {
              throw new Error("No image returned from 3D image generation API");
            }
            
            // Save the image (which is a Blob)
            // Detect the actual image format (JPEG or PNG)
            const imageBuffer = await image.arrayBuffer();
            const format = detectImageFormat(Buffer.from(imageBuffer));
            const extension = format === "JPEG" ? "jpg" : "png";
            
            await log('DEBUG', `Detected 3D image format: ${format}, using extension: ${extension}`);
            const saveResult = await saveFileFromData(image, "3d_image", extension, toolName);
            const imagePath = saveResult.filePath;
            await log('INFO', `3D image generated at: ${imagePath}`);
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Initial image generated', path: imagePath });
            
            // Step 2: Process the image with InstantMesh using the correct multi-step process
            
            // 2.1: Check if the image is valid
            await log('DEBUG', "Validating image for 3D conversion...");
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Validating image' });
            const imageFile = await fs.readFile(imagePath);
            const checkResult = await retryWithBackoff(async () => {
              // Use different endpoints based on the detected space type
              if (detectedSpaceType === SPACE_TYPE.INSTANTMESH) {
                return await modelClient.predict("/check_input_image", [
                  new File([imageFile], path.basename(imagePath), { type: "image/png" })
                ]);
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D || detectedSpaceType === SPACE_TYPE.HUNYUAN3D_MINI_TURBO) {
                // Hunyuan3D and Hunyuan3D-2mini-Turbo don't have a check_input_image endpoint,
                // so we'll just return a success
                await log('INFO', `Using ${detectedSpaceType} space - skipping image validation step`);
                return true;
              } else {
                throw new Error("Unknown space type detected. Cannot proceed with 3D asset generation.");
              }
            }, operationId);
            
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Image validation complete' });
            
            // 2.2: Preprocess the image (with background removal)
            await log('DEBUG', "Preprocessing image...");
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Preprocessing image' });
            const preprocessResult = await retryWithBackoff(async () => {
              // Use different endpoints based on the detected space type
              if (detectedSpaceType === SPACE_TYPE.INSTANTMESH) {
                return await modelClient.predict("/preprocess", [
                  new File([imageFile], path.basename(imagePath), { type: "image/png" }),
                  model3dRemoveBackground // Use configured value
                ]);
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D || detectedSpaceType === SPACE_TYPE.HUNYUAN3D_MINI_TURBO) {
                // Neither Hunyuan3D nor Hunyuan3D-2mini-Turbo have a preprocess endpoint,
                // but they have built-in background removal
                await log('INFO', `Using ${detectedSpaceType} space - using built-in background removal`);
                // Return the original image as we'll handle it in the next step
                return { data: imageFile };
              } else {
                throw new Error("Unknown space type detected. Cannot proceed with 3D asset generation.");
              }
            }, operationId);
            
            if (!preprocessResult || !preprocessResult.data) {
              throw new Error("Image preprocessing failed");
            }
            
            await log('DEBUG', "Successfully preprocessed image with InstantMesh");
            await log('DEBUG', "Preprocessed data type: " + typeof preprocessResult.data);
            
            // Save the preprocessed image and notify clients of resource change
            // Save the preprocessed image
            const processedResult = await saveFileFromData(
              preprocessResult.data,
              "3d_processed",
              "png",
              toolName
            );
            const processedImagePath = processedResult.filePath;
            await log('INFO', `Preprocessed image saved at: ${processedImagePath}`);
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Preprocessing complete', path: processedImagePath });
            
            // Notify clients that a new resource is available
            await notifyResourceListChanged();
            
            // 2.3: Generate multi-views
            await log('DEBUG', "Generating multi-views...");
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Generating multi-views' });
            const processedImageFile = await fs.readFile(processedImagePath);
            const mvsResult = await retryWithBackoff(async () => {
              // Use different endpoints based on the detected space type
              if (detectedSpaceType === SPACE_TYPE.INSTANTMESH) {
                // Use configured values or defaults for InstantMesh with validation
                // InstantMesh steps range: 30-75
                let steps = model3dSteps !== null ? model3dSteps : 75; // Default: 75
                steps = validateNumericRange(steps, 30, 75, 75, "InstantMesh steps");
                
                // Any integer is valid for seed, but use default if not provided
                const seed = model3dSeed !== null ? model3dSeed : 42; // Default: 42
                
                await log('INFO', `InstantMesh parameters - steps: ${steps}, seed: ${seed}`);
                
                return await modelClient.predict("/generate_mvs", [
                  new File([processedImageFile], path.basename(processedImagePath), { type: "image/png" }),
                  steps,
                  seed
                ]);
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D) {
                // Use configured values or defaults for Hunyuan3D-2 with validation
                // Hunyuan3D-2 steps range: 20-50
                let steps = model3dSteps !== null ? model3dSteps : 20; // Default: 20
                steps = validateNumericRange(steps, 20, 50, 20, "Hunyuan3D-2 steps");
                
                // Guidance scale already validated (0.0-100.0)
                const guidanceScale = model3dGuidanceScale !== null ? model3dGuidanceScale : 5.5; // Default: 5.5
                
                // Seed already validated (0-10000000)
                const seed = model3dSeed !== null ? model3dSeed : 1234; // Default: 1234
                
                // Validate octree resolution (valid options: "256", "384", "512")
                const validOctreeResolutions = ["256", "384", "512"];
                const octreeResolution = validateEnum(
                  model3dOctreeResolution,
                  validOctreeResolutions,
                  "256",
                  "Hunyuan3D-2 octree_resolution"
                );
                
                await log('INFO', `Hunyuan3D-2 parameters - steps: ${steps}, guidance_scale: ${guidanceScale}, seed: ${seed}, octree_resolution: ${octreeResolution}, remove_background: ${model3dRemoveBackground}`);
                
                // Hunyuan3D uses generation_all instead of generate_mvs
                await log('INFO', "Using Hunyuan3D space - using generation_all endpoint");
                return await modelClient.predict("/generation_all", [
                  prompt,
                  new File([processedImageFile], path.basename(processedImagePath), { type: "image/png" }),
                  steps,
                  guidanceScale,
                  seed,
                  octreeResolution,
                  model3dRemoveBackground
                ]);
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D_MINI_TURBO) {
                // Use configured values or defaults for Hunyuan3D-2mini-Turbo with validation
                
                // Determine default steps based on the selected mode
                let defaultSteps;
                if (model3dTurboMode === "Turbo") {
                  defaultSteps = 5; // Default for Turbo mode
                } else if (model3dTurboMode === "Fast") {
                  defaultSteps = 10; // Default for Fast mode
                } else { // Standard mode
                  defaultSteps = 20; // Default for Standard mode
                }
                
                // Hunyuan3D-2mini-Turbo steps range: 1-100
                let steps = model3dSteps !== null ? model3dSteps : defaultSteps;
                steps = validateNumericRange(steps, 1, 100, defaultSteps, "Hunyuan3D-2mini-Turbo steps");
                
                // Guidance scale already validated (0.0-100.0)
                const guidanceScale = model3dGuidanceScale !== null ? model3dGuidanceScale : 5.0; // Default: 5.0
                
                // Seed already validated (0-10000000)
                const seed = model3dSeed !== null ? model3dSeed : 1234; // Default: 1234
                
                // Validate octree resolution (range: 16-512)
                let octreeResolution = model3dOctreeResolution !== null ? parseInt(model3dOctreeResolution) : 256; // Default: 256
                octreeResolution = validateNumericRange(octreeResolution, 16, 512, 256, "Hunyuan3D-2mini-Turbo octree_resolution");
                
                // Validate num_chunks (range: 1000-5000000)
                const numChunks = validateNumericRange(8000, 1000, 5000000, 8000, "Hunyuan3D-2mini-Turbo num_chunks");
                
                await log('INFO', `Hunyuan3D-2mini-Turbo parameters - mode: ${model3dTurboMode}, steps: ${steps}, guidance_scale: ${guidanceScale}, seed: ${seed}, octree_resolution: ${octreeResolution}, remove_background: ${model3dRemoveBackground}, num_chunks: ${numChunks}`);
                
                // First, set the generation mode if specified
                if (model3dTurboMode) {
                  try {
                    await modelClient.predict("/on_gen_mode_change", [model3dTurboMode]);
                    await log('INFO', `Set generation mode to ${model3dTurboMode}`);
                  } catch (error) {
                    await log('WARN', `Failed to set generation mode: ${error.message}`);
                    // Continue with the generation even if setting the mode fails
                  }
                }
                
                // Use generation_all endpoint
                await log('INFO', "Using Hunyuan3D-2mini-Turbo space - using generation_all endpoint");
                
                // Hunyuan3D-2mini-Turbo has different parameters than Hunyuan3D-2
                return await modelClient.predict("/generation_all", [
                  prompt, // caption
                  new File([processedImageFile], path.basename(processedImagePath), { type: "image/png" }),
                  null, null, null, null, // Multi-view images (front, back, left, right)
                  steps,
                  guidanceScale,
                  seed,
                  octreeResolution,
                  model3dRemoveBackground,
                  numChunks, // num_chunks with validation
                  true // randomize_seed
                ]);
              } else {
                throw new Error("Unknown space type detected. Cannot proceed with 3D asset generation.");
              }
            }, operationId);
            
            if (!mvsResult || !mvsResult.data) {
              throw new Error("Multi-view generation failed");
            }
            
            await log('DEBUG', "Successfully generated multi-view image");
            await log('DEBUG', "Multi-view data type: " + typeof mvsResult.data);
            
            // Save the multi-view image and notify clients of resource change
            // Save the multi-view image
            const mvsResult2 = await saveFileFromData(
              mvsResult.data,
              "3d_multiview",
              "png",
              toolName
            );
            const mvsImagePath = mvsResult2.filePath;
            await log('INFO', `Multi-view image saved at: ${mvsImagePath}`);
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Multi-view generation complete', path: mvsImagePath });
            
            // Notify clients that a new resource is available
            await notifyResourceListChanged();
            
            // 2.4: Generate 3D models (OBJ and GLB)
            await log('DEBUG', "Generating 3D models...");
            await logOperation(toolName, operationId, 'PROCESSING', { step: 'Generating 3D models' });
            
            // This step is particularly prone to GPU quota errors, so use retry with backoff
            const modelResult = await retryWithBackoff(async () => {
              // Use different endpoints based on the detected space type
              if (detectedSpaceType === SPACE_TYPE.INSTANTMESH) {
                return await modelClient.predict("/make3d", []);
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D) {
                // Hunyuan3D-2 doesn't need a separate make3d step
                // as generation_all already returns the 3D model
                await log('INFO', `Using ${detectedSpaceType} space - 3D model already generated in previous step`);
                // Return the result from the previous step
                // For Hunyuan3D-2, the textured mesh URL is at result.data[1].url
                await log('DEBUG', `Hunyuan3D-2: Extracting textured mesh from result.data[1].url`);
                return mvsResult;
              } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D_MINI_TURBO) {
                // Hunyuan3D-2mini-Turbo doesn't need a separate make3d step
                // as generation_all already returns the 3D model
                await log('INFO', `Using ${detectedSpaceType} space - 3D model already generated in previous step`);
                // Return the result from the previous step
                // For Hunyuan3D-2mini-Turbo, the textured mesh URL is at result.data[1].value.url
                await log('DEBUG', `Hunyuan3D-2mini-Turbo: Extracting textured mesh from result.data[1].value.url`);
                return mvsResult;
              } else {
                throw new Error("Unknown space type detected. Cannot proceed with 3D asset generation.");
              }
            }, operationId, 5); // Pass operationId and more retries for this critical step
            
            if (!modelResult || !modelResult.data || !modelResult.data.length) {
              throw new Error("3D model generation failed");
            }
            
            await log('DEBUG', "Successfully generated 3D models");
            await log('DEBUG', "Model data type: " + typeof modelResult.data);
            
            // Save debug information for troubleshooting
            const modelDebugFilename = generateUniqueFilename("model_data", "json");
            const modelDebugPath = path.join(assetsDir, modelDebugFilename);
            await fs.writeFile(modelDebugPath, JSON.stringify(modelResult, null, 2));
            await log('DEBUG', `Model data saved as JSON at: ${modelDebugPath}`);
            // Extract the model data based on the space type
            let objModelData, glbModelData;
            
            if (detectedSpaceType === SPACE_TYPE.INSTANTMESH) {
              // InstantMesh returns both OBJ and GLB formats
              objModelData = modelResult.data[0];
              glbModelData = modelResult.data[1];
              await log('DEBUG', `InstantMesh: Using modelResult.data[0] for OBJ and modelResult.data[1] for GLB`);
            } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D) {
              // For Hunyuan3D-2, we want the textured mesh which is at index 1
              // We'll use it for both OBJ and GLB since we primarily want the textured version
              objModelData = modelResult.data[1]; // Textured mesh
              glbModelData = modelResult.data[1]; // Textured mesh
              await log('DEBUG', `Hunyuan3D-2: Using textured mesh from modelResult.data[1] for both OBJ and GLB`);
            } else if (detectedSpaceType === SPACE_TYPE.HUNYUAN3D_MINI_TURBO) {
              // For Hunyuan3D-2mini-Turbo, the textured mesh is at index 1 but nested in value
              // We need to ensure we're accessing it correctly
              if (modelResult.data[1] && modelResult.data[1].value) {
                objModelData = modelResult.data[1].value; // Textured mesh
                glbModelData = modelResult.data[1].value; // Textured mesh
                await log('DEBUG', `Hunyuan3D-2mini-Turbo: Using textured mesh from modelResult.data[1].value for both OBJ and GLB`);
              } else {
                // Fallback to white mesh if textured mesh is not available
                objModelData = modelResult.data[0];
                glbModelData = modelResult.data[0];
                await log('WARN', `Hunyuan3D-2mini-Turbo: Textured mesh not found, falling back to white mesh`);
              }
            } else {
              throw new Error(`Unknown space type: ${detectedSpaceType}`);
            }
            
            // Save both model formats
            // Save both model formats
            // Save both model formats and notify clients of resource changes
            const objResult = await saveFileFromData(objModelData, "3d_model", "obj", toolName);
            await log('INFO', `OBJ model saved at: ${objResult.filePath}`);
            
            // Notify clients that a new resource is available
            await notifyResourceListChanged();
            
            const glbResult = await saveFileFromData(glbModelData, "3d_model", "glb", toolName);
            await log('INFO', `GLB model saved at: ${glbResult.filePath}`);
            
            // Notify clients that a new resource is available
            await notifyResourceListChanged();
            
            // Create a completion message with detailed information
            const completionMessage = `3D asset generation complete (Operation ID: ${operationId}).\n\n` +
                                     `Process completed in ${Math.round((Date.now() - new Date(global.operationUpdates[operationId][0].timestamp).getTime()) / 1000)} seconds.\n\n` +
                                     `3D models available at:\n` +
                                     `- OBJ: ${objResult.resourceUri}\n` +
                                     `- GLB: ${glbResult.resourceUri}\n\n` +
                                     `You can view these models in any 3D viewer that supports OBJ or GLB formats.`;
            
            await logOperation(toolName, operationId, 'COMPLETED', {
              objPath: objResult.filePath,
              glbPath: glbResult.filePath,
              objUri: objResult.resourceUri,
              glbUri: glbResult.resourceUri,
              processingTime: `${Math.round((Date.now() - new Date(global.operationUpdates[operationId][0].timestamp).getTime()) / 1000)} seconds`
            });
            
            // Here you would typically send the final response to the client
            // Since we're already returning the initial response, we'll log the completion
            await log('INFO', `Operation ${operationId} completed successfully. Final response ready.`);
            
            // In a real-world scenario, you would send this completion message to the client
            // For example, through a WebSocket connection or by updating a status endpoint
            // For now, we'll just log it
            await log('INFO', `Completion message for client:\n${completionMessage}`);
            
          } catch (error) {
            const errorMessage = `Error in 3D asset generation (Operation ID: ${operationId}):\n${error.message}\n\nThe operation has been terminated. Please try again later or with a different prompt.`;
            
            await log('ERROR', `Error in background processing for operation ${operationId}: ${error.message}`);
            await logOperation(toolName, operationId, 'ERROR', {
              error: error.message,
              stack: error.stack,
              phase: global.operationUpdates[operationId] ?
                     global.operationUpdates[operationId][global.operationUpdates[operationId].length - 1].status :
                     'UNKNOWN'
            });
            
            // Here you would typically send an error response to the client
            // Since we're already returning the initial response, we'll log the error
            await log('ERROR', `Operation ${operationId} failed: ${error.message}`);
            
            // In a real-world scenario, you would send this error message to the client
            // For example, through a WebSocket connection or by updating a status endpoint
            // For now, we'll just log it
            await log('INFO', `Error message for client:\n${errorMessage}`);
          }
        })();
        
        // Return the initial response immediately to prevent timeout
        return initialResponse;
      } catch (error) {
        await log('ERROR', `Error starting operation ${operationId}: ${error.message}`);
        await logOperation(toolName, operationId, 'ERROR', { error: error.message });
        return {
          content: [{ type: "text", text: `Error starting 3D asset generation: ${error.message}` }],
          isError: true
        };
      }
    }

    throw {
      code: MCP_ERROR_CODES.MethodNotFound,
      message: `Unknown tool: ${toolName}`
    };
  } catch (error) {
    // Handle different types of errors with appropriate MCP error codes
    let errorCode = MCP_ERROR_CODES.InternalError;
    let errorMessage = error.message || "Unknown error";
    
    if (error.code) {
      // If the error already has a code, use it
      errorCode = error.code;
      errorMessage = error.message;
    } else if (error instanceof z.ZodError) {
      // Validation errors
      errorCode = MCP_ERROR_CODES.InvalidParams;
      errorMessage = `Invalid parameters: ${error.errors.map(e => e.message).join(", ")}`;
    }
    
    await log('ERROR', `Error in ${toolName}: ${errorMessage} (Code: ${errorCode})`);
    return {
      content: [{ type: "text", text: `Error: ${errorMessage}` }],
      isError: true,
      errorCode: errorCode
    };
  }
});
// Helper function to sanitize prompts
function sanitizePrompt(prompt) {
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

// Generate a unique filename to prevent conflicts
function generateUniqueFilename(prefix, ext, toolName) {
  const timestamp = Date.now();
  const uniqueId = crypto.randomBytes(4).toString('hex');
  return `${prefix}_${toolName}_${timestamp}_${uniqueId}.${ext}`;
}

// Parse resource URI templates
function parseResourceUri(uri) {
  // Support for templated URIs like asset://{type}/{id}
  const match = uri.match(/^asset:\/\/(?:([^\/]+)\/)?(.+)$/);
  if (!match) return null;
  
  const [, type, id] = match;
  return { type, id };
}

// Helper to get MIME type from filename
function getMimeType(filename) {
  if (filename.endsWith(".png")) return "image/png";
  if (filename.endsWith(".jpg") || filename.endsWith(".jpeg")) return "image/jpeg";
  if (filename.endsWith(".obj")) return "model/obj";
  if (filename.endsWith(".glb")) return "model/gltf-binary";
  return "application/octet-stream"; // Default
}

// Helper to detect image format from buffer
function detectImageFormat(buffer) {
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
async function saveFileFromUrl(url, prefix, ext, toolName) {
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
    await log('ERROR', `Error saving file from URL: ${error.message}`);
    throw new Error("Failed to save file from URL");
  }
}

// Helper to save files from data (blob, base64, etc.)
async function saveFileFromData(data, prefix, ext, toolName) {
  if (!data) {
    throw new Error("No data provided to save");
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
      await log('DEBUG', "Saving data as Blob/File");
      const arrayBuffer = await data.arrayBuffer();
      await fs.writeFile(filePath, Buffer.from(arrayBuffer));
    } else if (typeof data === 'string') {
      // Check if it's base64 encoded
      if (data.match(/^data:[^;]+;base64,/)) {
        await log('DEBUG', "Saving data as base64 string");
        const base64Data = data.split(',')[1];
        await fs.writeFile(filePath, Buffer.from(base64Data, 'base64'));
      } else {
        await log('DEBUG', "Saving data as regular string");
        await fs.writeFile(filePath, data);
      }
    } else if (data instanceof ArrayBuffer || ArrayBuffer.isView(data)) {
      await log('DEBUG', "Saving data as ArrayBuffer");
      await fs.writeFile(filePath, Buffer.from(data));
    } else if (Array.isArray(data) && data.length > 0) {
      // Handle array of file data (common in InstantMesh API responses)
      await log('DEBUG', "Data is an array with " + data.length + " items");
      const fileData = data[0];
      
      if (fileData.url) {
        await log('DEBUG', "Found URL in data: " + fileData.url);
        // Fetch the file from the URL with authentication
        const headers = { Authorization: `Bearer ${hfToken}` };
        await log('DEBUG', "Adding HF token authentication to URL fetch request");
        
        // Verify the URL domain matches the expected domain for the configured space
        try {
          const urlDomain = new URL(fileData.url).hostname;
          const expectedDomain = modelSpace.split('/')[0].toLowerCase() + '-' + modelSpace.split('/')[1].toLowerCase() + '.hf.space';
          
          if (urlDomain !== expectedDomain) {
            await log('WARN', `URL domain mismatch: Expected "${expectedDomain}" but got "${urlDomain}". This suggests your MODEL_SPACE setting doesn't match the space that generated this URL.`);
          }
        } catch (urlError) {
          await log('WARN', `Error parsing URL: ${urlError.message}`);
        }
        
        const response = await fetch(fileData.url, { headers });
        if (!response.ok) {
          throw new Error(`Failed to fetch file: ${response.status} ${response.statusText}`);
        }
        
        const buffer = await response.arrayBuffer();
        await fs.writeFile(filePath, Buffer.from(buffer));
        await log('DEBUG', "Successfully saved file from URL");
      } else {
        await log('DEBUG', "No URL found in array data, saving as JSON");
        await fs.writeFile(filePath, JSON.stringify(data));
      }
    } else if (typeof data === 'object' && data.url) {
      // Handle object with URL property
      await log('DEBUG', "Data is an object with URL: " + data.url);
      // Fetch the file from the URL with authentication
      const headers = { Authorization: `Bearer ${hfToken}` };
      await log('DEBUG', "Adding HF token authentication to URL fetch request");
      
      // Verify the URL domain matches the expected domain for the configured space
      try {
        const urlDomain = new URL(data.url).hostname;
        const expectedDomain = modelSpace.split('/')[0].toLowerCase() + '-' + modelSpace.split('/')[1].toLowerCase() + '.hf.space';
        
        if (urlDomain !== expectedDomain) {
          await log('WARN', `URL domain mismatch: Expected "${expectedDomain}" but got "${urlDomain}". This suggests your MODEL_SPACE setting doesn't match the space that generated this URL.`);
        }
      } catch (urlError) {
        await log('WARN', `Error parsing URL: ${urlError.message}`);
      }
      
      const response = await fetch(data.url, { headers });
      if (!response.ok) {
        throw new Error(`Failed to fetch file: ${response.status} ${response.statusText}`);
      }
      
      const buffer = await response.arrayBuffer();
      await fs.writeFile(filePath, Buffer.from(buffer));
      await log('DEBUG', "Successfully saved file from URL");
    } else if (typeof data === 'object') {
      // JSON or other object
      await log('DEBUG', "Saving data as JSON object");
      await fs.writeFile(filePath, JSON.stringify(data));
    } else {
      // Fallback
      await log('DEBUG', "Saving data using fallback method");
      await fs.writeFile(filePath, Buffer.from(String(data)));
    }
    
    // Return both the file path and the resource URI
    return {
      filePath,
      resourceUri: `asset://${filename}`
    };
  } catch (error) {
    // Provide more detailed error messages for common issues
    if (error.message.includes("404") || error.message.includes("Not Found")) {
      await log('ERROR', `Error saving file from data: ${error.message}. This may be due to an incorrect model space configuration. Please check your MODEL_SPACE environment variable.`);
      
      // Check if the URL contains a domain that doesn't match the configured space
      if (typeof data === 'object' && (data.url || (Array.isArray(data) && data[0]?.url))) {
        const url = data.url || (Array.isArray(data) ? data[0].url : null);
        if (url) {
          try {
            const urlDomain = new URL(url).hostname;
            const expectedDomain = modelSpace.split('/')[0].toLowerCase() + '-' + modelSpace.split('/')[1].toLowerCase() + '.hf.space';
            
            if (urlDomain !== expectedDomain) {
              await log('ERROR', `URL domain mismatch: Expected "${expectedDomain}" but got "${urlDomain}". This suggests your MODEL_SPACE setting doesn't match the space that generated this URL.`);
              await log('INFO', `To fix this issue, please update your MODEL_SPACE in .env to match the space name in the URL, or duplicate the space correctly and update your configuration.`);
            }
          } catch (urlError) {
            await log('ERROR', `Error parsing URL: ${urlError.message}`);
          }
        }
      }
    } else if (error.message.includes("401") || error.message.includes("unauthorized")) {
      await log('ERROR', `Error saving file from data: ${error.message}. This may be due to authentication issues. Please check your HF_TOKEN environment variable.`);
    } else {
      await log('ERROR', `Error saving file from data: ${error.message}`);
    }
    
    // Save debug information for troubleshooting
    try {
      const debugFilename = generateUniqueFilename("debug_data", "json");
      const debugPath = path.join(assetsDir, debugFilename);
      let debugData;
      
      if (typeof data === 'object') {
        debugData = JSON.stringify(data, null, 2);
      } else {
        debugData = String(data);
      }
      
      await fs.writeFile(debugPath, debugData);
      await log('INFO', `Debug data saved at: ${debugPath}`);
    } catch (debugError) {
      await log('ERROR', `Failed to save debug data: ${debugError.message}`);
    }
    
    throw new Error("Failed to save file from data");
  }
}

// Resource listing (for file management)
server.setRequestHandler(ListResourcesRequestSchema, async (request) => {
  await log('INFO', "Listing resources");
  
  try {
    // Check if there's a filter in the request
    const uriTemplate = request.params?.uriTemplate;
    let typeFilter = null;
    
    if (uriTemplate) {
      const templateMatch = uriTemplate.match(/^asset:\/\/([^\/]+)\/.*$/);
      if (templateMatch) {
        typeFilter = templateMatch[1];
        await log('INFO', `Filtering resources by type: ${typeFilter}`);
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
    await log('ERROR', `Error listing resources: ${error.message}`);
    return { resources: [] };
  }
});

// Resource read handler
server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  const uri = request.params.uri;
  await log('INFO', `Reading resource: ${uri}`);
  
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
      await log('ERROR', `Error reading resource: ${error.message}`);
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

// Prompt handlers
server.setRequestHandler(ListPromptsRequestSchema, async () => {
  return {
    prompts: [
      {
        name: "generate_2d_sprite",
        description: "Generate a 2D sprite from a description",
        arguments: [{ name: "prompt", description: "Sprite description", required: true }]
      },
      {
        name: "generate_3d_model",
        description: "Generate a 3D model from a description",
        arguments: [{ name: "prompt", description: "Model description", required: true }]
      }
    ]
  };
});

// Define Zod schemas for prompt argument validation
const promptSchema2D = z.object({
  prompt: z.string().min(1).max(500).transform(val => sanitizePrompt(val))
});

const promptSchema3D = z.object({
  prompt: z.string().min(1).max(500).transform(val => sanitizePrompt(val))
});

server.setRequestHandler(GetPromptRequestSchema, async (request) => {
  const promptName = request.params.name;
  const args = request.params.arguments;
  
  try {
    if (promptName === "generate_2d_sprite") {
      // Validate arguments using Zod schema
      const { prompt } = promptSchema2D.parse(args);
      
      return {
        description: "Generate a 2D sprite",
        messages: [
          {
            role: "user",
            content: { type: "text", text: `Generate a 2D sprite: ${prompt}, high detailed, complete object, not cut off, white solid background` }
          }
        ]
      };
    }
    
    if (promptName === "generate_3d_model") {
      // Validate arguments using Zod schema
      const { prompt } = promptSchema3D.parse(args);
      
      return {
        description: "Generate a 3D model",
        messages: [
          {
            role: "user",
            content: { type: "text", text: `Generate a 3D model: ${prompt}, high detailed, complete object, not cut off, white solid background` }
          }
        ]
      };
    }
    
    // If prompt not found, throw an error with MCP error code
    throw {
      code: MCP_ERROR_CODES.MethodNotFound,
      message: `Prompt not found: ${promptName}`
    };
  } catch (error) {
    // Handle different types of errors
    if (error.code) {
      // If the error already has a code, rethrow it
      throw error;
    } else if (error instanceof z.ZodError) {
      // Validation errors
      throw {
        code: MCP_ERROR_CODES.InvalidParams,
        message: `Invalid arguments: ${error.errors.map(e => e.message).join(", ")}`
      };
    } else {
      // Other errors
      throw {
        code: MCP_ERROR_CODES.InternalError,
        message: `Internal error: ${error.message}`
      };
    }
  }
});

// Start the server
async function main() {
  // Check if we should use SSE transport (for remote access)
  const useSSE = process.argv.includes("--sse");
  const useHttps = process.argv.includes("--https");
  
  if (useSSE) {
    // Setup Express server for SSE transport
    const app = express();
    const port = process.env.PORT || 3000;
    
    // Store transports by client ID for multi-connection support
    global.transports = new Map();
    
    // Add health check endpoint
    app.get("/health", (req, res) => {
      res.status(200).json({
        status: "ok",
        timestamp: new Date().toISOString(),
        version: "0.3.0", // Updated to version 0.3.0
        uptime: process.uptime()
      });
    });
    
    app.get("/sse", async (req, res) => {
      const clientId = req.query.clientId || crypto.randomUUID();
      await log('INFO', `SSE connection established for client: ${clientId}`);
      
      // Set headers for SSE
      res.setHeader('Content-Type', 'text/event-stream');
      res.setHeader('Cache-Control', 'no-cache');
      res.setHeader('Connection', 'keep-alive');
      
      // Create a new transport for this client
      const transport = new SSEServerTransport("/messages", res);
      global.transports.set(clientId, transport);
      
      // Handle client disconnect
      req.on('close', () => {
        global.transports.delete(clientId);
        log('INFO', `Client ${clientId} disconnected`);
      });
      
      await server.connect(transport);
      
      // Send initial connection confirmation
      res.write(`data: ${JSON.stringify({ connected: true, clientId })}\n\n`);
    });
    
    app.post("/messages", express.json(), async (req, res) => {
      const clientId = req.headers['x-client-id'] || 'anonymous';
      
      // Apply rate limiting
      if (!checkRateLimit(clientId)) {
        res.status(429).json({ error: "Too many requests" });
        return;
      }
      
      // Get the transport for this client
      const transport = global.transports.get(clientId);
      if (!transport) {
        res.status(404).json({ error: "Client not connected" });
        return;
      }
      
      await transport.handlePostMessage(req, res);
    });
    
    // Use HTTPS if requested
    if (useHttps) {
      try {
        // Check for SSL certificate files
        const sslDir = path.join(process.cwd(), 'ssl');
        const keyPath = path.join(sslDir, 'key.pem');
        const certPath = path.join(sslDir, 'cert.pem');
        
        // Create ssl directory if it doesn't exist
        await fs.mkdir(sslDir, { recursive: true });
        
        // Check if SSL files exist, if not, generate self-signed certificate
        let key, cert;
        try {
          key = await fs.readFile(keyPath);
          cert = await fs.readFile(certPath);
          await log('INFO', "Using existing SSL certificates");
        } catch (error) {
          await log('WARN', "SSL certificates not found, please create them manually");
          await log('INFO', "You can generate self-signed certificates with:");
          await log('INFO', "openssl req -x509 -newkey rsa:4096 -keyout ssl/key.pem -out ssl/cert.pem -days 365 -nodes");
          throw new Error("SSL certificates required for HTTPS");
        }
        
        const httpsServer = https.createServer({ key, cert }, app);
        httpsServer.listen(port, () => {
          log('INFO', `MCP Game Asset Generator running with HTTPS SSE transport on port ${port}`);
        });
      } catch (error) {
        await log('ERROR', `HTTPS setup failed: ${error.message}`);
        await log('WARN', "Falling back to HTTP");
        app.listen(port, () => {
          log('INFO', `MCP Game Asset Generator running with HTTP SSE transport on port ${port}`);
        });
      }
    } else {
      // Standard HTTP server
      app.listen(port, () => {
        log('INFO', `MCP Game Asset Generator running with HTTP SSE transport on port ${port}`);
      });
    }
  } else {
    // Use stdio transport for local access (e.g., Claude Desktop)
    const transport = new StdioServerTransport();
    await server.connect(transport);
    await log('INFO', "MCP Game Asset Generator running with stdio transport");
    
    // Add health check handler for stdio transport
    server.setRequestHandler(z.object({ method: z.literal("health/check") }), async () => {
      return {
        status: "ok",
        timestamp: new Date().toISOString(),
        version: "0.3.0", // Updated to version 0.3.0
        uptime: process.uptime()
      };
    });
  }
}

main().catch((err) => {
  console.error("Server error:", err);
  process.exit(1);
});