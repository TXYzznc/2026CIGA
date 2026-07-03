import { promises as fs } from "fs";
import path from "path";

export async function log(level = "INFO", message, workDir) {
  const timestamp = new Date().toISOString();
  const logMessage = `[${level.toUpperCase()}] ${timestamp} - ${message}\n`;
  console.error(logMessage.trim());

  try {
    const logDir = path.join(workDir, "logs");
    await fs.mkdir(logDir, { recursive: true });
    const logFile = path.join(logDir, "server.log");
    await fs.appendFile(logFile, logMessage);
  } catch (err) {
    console.error(`Failed to write to log file: ${err}`);
  }
}

export async function logOperation(toolName, operationId, status, details = {}, workDir) {
  const level = status === "ERROR" ? "ERROR" : "INFO";
  const detailsStr = Object.entries(details)
    .map(([key, value]) => `${key}: ${value}`)
    .join(", ");
  const logMessage = `Operation ${operationId} [${toolName}] - ${status}${detailsStr ? " - " + detailsStr : ""}`;
  await log(level, logMessage, workDir);

  if (!global.operationUpdates[operationId]) {
    global.operationUpdates[operationId] = [];
  }
  global.operationUpdates[operationId].push({
    status,
    details,
    timestamp: new Date().toISOString(),
    message: logMessage,
  });

  if (global.operationUpdates[operationId].length > 100) {
    global.operationUpdates[operationId].shift();
  }
}

/**
 * Enhanced error logging function that captures detailed information about errors
 * @param {Error} error - The error object
 * @param {string} operationId - The ID of the operation that failed
 * @param {string} workDir - The working directory for log files
 * @param {Object} contextInfo - Additional context information about the operation
 */
export async function logDetailedError(error, operationId, workDir, contextInfo = {}) {
  // Log the basic error message
  await log('ERROR', `Error in operation ${operationId}: ${error.message}`, workDir);
  
  // Log the stack trace if available
  if (error.stack) {
    await log('ERROR', `Stack trace for operation ${operationId}:\n${error.stack}`, workDir);
  }
  
  // Log API response data if available
  if (error.response) {
    await log('ERROR', `API response status for operation ${operationId}: ${error.response.status}`, workDir);
    try {
      const responseData = typeof error.response.data === 'object'
        ? JSON.stringify(error.response.data, null, 2)
        : error.response.data;
      await log('ERROR', `API response data for operation ${operationId}:\n${responseData}`, workDir);
    } catch (jsonError) {
      await log('ERROR', `API response data (non-JSON) for operation ${operationId}: ${error.response.data}`, workDir);
    }
  }
  
  // Log any additional context information
  if (Object.keys(contextInfo).length > 0) {
    const contextStr = Object.entries(contextInfo)
      .map(([key, value]) => {
        // Handle objects by converting them to JSON strings
        if (typeof value === 'object' && value !== null) {
          try {
            return `${key}: ${JSON.stringify(value)}`;
          } catch (e) {
            return `${key}: [Complex Object]`;
          }
        }
        return `${key}: ${value}`;
      })
      .join('\n');
    await log('ERROR', `Context information for operation ${operationId}:\n${contextStr}`, workDir);
  }
  
  // Log to operation updates
  await logOperation('ERROR', operationId, 'ERROR', {
    error: error.message,
    stack: error.stack ? 'See logs for details' : 'Not available',
    context: 'See logs for details',
    timestamp: new Date().toISOString()
  }, workDir);
}