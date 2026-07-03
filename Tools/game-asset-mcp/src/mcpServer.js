import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { z } from "zod";
import { registerToolHandlers } from "./tools.js";
import { registerResourceHandlers } from "./resources.js";
import { registerPromptHandlers } from "./prompts.js";
import { log } from "./logger.js";

export async function createServer(config, clients) {
  const server = new Server(
    { name: "game-asset-generator", version: "0.3.0" }, // Updated to version 0.3.0 with Hunyuan3D-2mini-Turbo support
    {
      capabilities: {
        tools: { list: true, call: true },
        resources: { list: true, read: true, listChanged: true }, // Added listChanged capability
        prompts: { list: true, get: true }
      },
    }
  );

  const notifyResourceListChanged = async () => {
    await log("DEBUG", "Notifying clients of resource list change", config.workDir);
    await server.notification({ method: "notifications/resources/list_changed" });
    if (global.transports) {
      for (const [clientId, transport] of global.transports) {
        try {
          await transport.sendNotification({ method: "notifications/resources/list_changed" });
          await log('DEBUG', `Sent resource list change notification to client ${clientId}`, config.workDir);
        } catch (error) {
          await log('ERROR', `Failed to send notification to client ${clientId}: ${error.message}`, config.workDir);
        }
      }
    }
  };

  registerToolHandlers(server, config, clients, notifyResourceListChanged);
  registerResourceHandlers(server, config);
  registerPromptHandlers(server);

  return server;
}