#!/usr/bin/env node

import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { SSEServerTransport } from "@modelcontextprotocol/sdk/server/sse.js";
import express from "express";
import https from "https";
import { promises as fs } from "fs";
import path from "path";
import crypto from "crypto";
import { z } from "zod";
import { loadConfig } from "./config.js";
import { log } from "./logger.js";
import { createServer } from "./mcpServer.js";
import { checkRateLimit } from "./utils.js";
import { initializeClients } from "./clients.js";

// Initialize global operation updates
global.operationUpdates = {};

async function main() {
  const config = await loadConfig();
  await log("INFO", "Configuration loaded", config.workDir);
  
  try {
    const clients = await initializeClients(config);
    const server = await createServer(config, clients);
    const useSSE = process.argv.includes("--sse");
    const useHttps = process.argv.includes("--https");

    if (useSSE) {
      const app = express();
      const port = config.port;
      
      // Store transports by client ID for multi-connection support
      global.transports = new Map();
      
      // Rate limiting is now handled internally in utils.js
      
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
        await log("INFO", `SSE connection established for client: ${clientId}`, config.workDir);
        
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
          log('INFO', `Client ${clientId} disconnected`, config.workDir);
        });
        
        await server.connect(transport);
        
        // Send initial connection confirmation
        res.write(`data: ${JSON.stringify({ connected: true, clientId })}\n\n`);
      });

      app.post("/messages", express.json(), async (req, res) => {
        const clientId = req.headers['x-client-id'] || 'anonymous';
        // Apply rate limiting
        if (!checkRateLimit(clientId, 10, 60000)) {
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

      if (useHttps) {
        try {
          // Check for SSL certificate files
          const sslDir = path.join(process.cwd(), 'ssl');
          const keyPath = path.join(sslDir, 'key.pem');
          const certPath = path.join(sslDir, 'cert.pem');
          
          // Create ssl directory if it doesn't exist
          await fs.mkdir(sslDir, { recursive: true });
          
          // Check if SSL files exist
          let key, cert;
          try {
            key = await fs.readFile(keyPath);
            cert = await fs.readFile(certPath);
            await log('INFO', "Using existing SSL certificates", config.workDir);
          } catch (error) {
            await log('WARN', "SSL certificates not found, please create them manually", config.workDir);
            await log('INFO', "You can generate self-signed certificates with:", config.workDir);
            await log('INFO', "openssl req -x509 -newkey rsa:4096 -keyout ssl/key.pem -out ssl/cert.pem -days 365 -nodes", config.workDir);
            throw new Error("SSL certificates required for HTTPS");
          }
          
          const httpsServer = https.createServer({ key, cert }, app);
          httpsServer.listen(port, () => {
            log('INFO', `MCP Game Asset Generator running with HTTPS SSE transport on port ${port}`, config.workDir);
          });
        } catch (error) {
          await log('ERROR', `HTTPS setup failed: ${error.message}`, config.workDir);
          await log('WARN', "Falling back to HTTP", config.workDir);
          app.listen(port, () => {
            log('INFO', `MCP Game Asset Generator running with HTTP SSE transport on port ${port}`, config.workDir);
          });
        }
      } else {
        // Standard HTTP server
        app.listen(port, () => {
          log('INFO', `MCP Game Asset Generator running with HTTP SSE transport on port ${port}`, config.workDir);
        });
      }
    } else {
      // Use stdio transport for local access (e.g., Claude Desktop)
      const transport = new StdioServerTransport();
      await server.connect(transport);
      await log('INFO', "MCP Game Asset Generator running with stdio transport", config.workDir);
      
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
  } catch (error) {
    await log('ERROR', `Initialization error: ${error.message}`, config.workDir);
    throw error;
  }
}

main().catch((err) => {
  console.error("Server error:", err);
  process.exit(1);
});