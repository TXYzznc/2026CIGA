import { ListPromptsRequestSchema, GetPromptRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import { sanitizePrompt } from "./utils.js";

const promptSchema = z.object({ prompt: z.string().min(1).max(500).transform(sanitizePrompt) });

export function registerPromptHandlers(server) {
  server.setRequestHandler(ListPromptsRequestSchema, async () => ({
    prompts: [
      {
        name: "generate_2d_sprite",
        description: "Generate a 2D sprite from a description",
        arguments: [{ name: "prompt", description: "Sprite description", required: true }],
      },
      {
        name: "generate_3d_model",
        description: "Generate a 3D model from a description",
        arguments: [{ name: "prompt", description: "Model description", required: true }],
      },
    ],
  }));

  server.setRequestHandler(GetPromptRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    const { prompt } = promptSchema.parse(args);
    if (name === "generate_2d_sprite" || name === "generate_3d_model") {
      return {
        description: `Generate a ${name.includes("2d") ? "2D sprite" : "3D model"}`,
        messages: [
          {
            role: "user",
            content: {
              type: "text",
              text: `Generate a ${name.includes("2d") ? "2D sprite" : "3D model"}: ${prompt}, high detailed, complete object, not cut off, white solid background`,
            },
          },
        ],
      };
    }
    throw { code: -32601, message: `Prompt not found: ${name}` };
  });
}