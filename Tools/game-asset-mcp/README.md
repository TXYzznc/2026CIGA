# Project owner will look into this repository soon, expect an update in JUNE 2026

# Game Asset Generator using MCP and Hugging Face Spaces

This project is an innovative tool that simplifies game asset creation by leveraging AI-powered generation. Whether you're a game developer seeking rapid prototypes or an AI enthusiast exploring generative models, this tool enables you to create **2D** and **3D game assets** from text prompts effortlessly. It integrates AI models from **Hugging Face Spaces**—powered by `"gokaygokay/Flux-2D-Game-Assets-LoRA"`, `"gokaygokay/Flux-Game-Assets-LoRA-v2"`, and one of three 3D model generation spaces (`InstantMesh`, `Hunyuan3D-2`, or `Hunyuan3D-2mini-Turbo`, which you must duplicate to your account)—and uses the **Model Context Protocol (MCP)** for seamless interaction with AI assistants like **Claude Desktop**.

<p align="center">
  <a href="https://pay.ziina.com/MubarakHAlketbi">
    <img src="https://img.shields.io/badge/Support_Me-Donate-9626ff?style=for-the-badge&logo=https%3A%2F%2Fimgur.com%2FvwC39JY" alt="Support Me - Donate">
  </a>
  <a href="https://github.com/RooVetGit/Roo-Code">
    <img src="https://img.shields.io/badge/Built_With-Roo_Code-412894?style=for-the-badge" alt="Built With - Roo Code">
  </a>
  <br>
  <a href="https://glama.ai/mcp/servers/@MubarakHAlketbi/game-asset-mcp">
    <img width="380" height="200" src="https://glama.ai/mcp/servers/@MubarakHAlketbi/game-asset-mcp/badge" />
  </a>
</p>

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Features](#features)
3. [How It Works](#how-it-works)
4. [Prerequisites](#prerequisites)
5. [Installation](#installation)
6. [Usage](#usage)
7. [Configuration](#configuration)
8. [File Management](#file-management)
9. [MCP Integration](#mcp-integration)
10. [Troubleshooting](#troubleshooting)
11. [Advanced](#advanced)
12. [Contributing](#contributing)
13. [License](#license)

---

## Project Overview

The **Game Asset Generator** (version **0.3.0**) harnesses AI to streamline the creation of game assets. It supports generating **2D assets** (e.g., pixel art sprites) and **3D assets** (e.g., OBJ and GLB models) from text prompts, integrating with **Hugging Face Spaces** and the **Model Context Protocol (MCP)**. This release introduces support for multiple 3D model generation spaces—`InstantMesh`, `Hunyuan3D-2`, and `Hunyuan3D-2mini-Turbo`—offering flexibility and enhanced performance. Built with **Node.js** and the **MCP TypeScript SDK (v1.7.0)**, it provides a robust, cross-platform solution for asset generation.

---

## Features

- **2D Asset Generation**: Create pixel art, sprites, or other 2D assets from text prompts (e.g., "pixel art sword").
- **3D Asset Generation**: Generate 3D models (OBJ and GLB formats) from text descriptions, with automatic image-to-model conversion.
- **Multiple 3D Model Spaces**: Supports `InstantMesh`, `Hunyuan3D-2`, and `Hunyuan3D-2mini-Turbo` for varied 3D generation workflows.
- **MCP Integration**: Seamlessly interact with the tool via MCP-compatible clients like **Claude Desktop**.
- **File Management**: Automatically saves and organizes assets in a local `assets` directory with resource URIs (e.g., `asset://{type}/{id}`).
- **Robust Input Validation**: Uses **Zod** for secure and reliable input processing.
- **Multi-Client Support**: Handles multiple simultaneous connections via **SSE transport**.
- **Secure Remote Access**: Optional **HTTPS** support for safe remote communication.
- **Extensible Backend**: Modular design for easy integration of new models or features.
- **Cross-Platform**: Compatible with Windows, macOS, and Linux using **Node.js**.
- **Configurable 3D Generation**: Customize parameters like inference steps, guidance scale, and turbo mode via environment variables.

---

## How It Works

The Game Asset Generator transforms text prompts into game-ready assets through an automated pipeline:

1. **User Input**: Submit a text prompt (e.g., "pixel art sword" or "isometric 3D castle").
2. **MCP Server**: Routes the prompt to the appropriate tool (`generate_2d_asset` or `generate_3d_asset`).
3. **AI Model Interaction**:
   - **2D Assets**: Utilizes the **Hugging Face Inference API** with `"gokaygokay/Flux-2D-Game-Assets-LoRA"` (50 steps).
   - **3D Assets**:
     - Generates an initial image using `"gokaygokay/Flux-Game-Assets-LoRA-v2"` (30 steps).
     - Converts the image to a 3D model using one of:
       - **InstantMesh**: Multi-step process (`/preprocess`, `/generate_mvs`, `/make3d`).
       - **Hunyuan3D-2**: Single-step process (`/generation_all`).
       - **Hunyuan3D-2mini-Turbo**: Single-step process (`/generation_all`) with configurable turbo modes.
4. **File Output**: Saves assets (PNG for 2D, OBJ/GLB for 3D) in the `assets` directory.
5. **Response**: Returns resource URIs (e.g., `asset://3d_model/filename.glb`) for immediate use.

### Workflow Diagram
```
User Prompt → MCP Server → AI Model(s) → Local File → Resource URI Response
```

Prompts are automatically enhanced with "high detailed, complete object, not cut off, white solid background" for optimal quality.

---

## Prerequisites

- **Node.js**: Version 16+ (includes `npm`).
- **Git**: For cloning the repository.
- **Internet Access**: Required for Hugging Face API connectivity.
- **Hugging Face Account**: Needed for API access; obtain your token from [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens).
- **NPM Packages**:
  - `@gradio/client`: Interacts with Hugging Face Spaces.
  - `@huggingface/inference`: For direct model inference.
  - `@modelcontextprotocol/sdk`: Implements the MCP server.
  - `dotenv`: Loads environment variables.
  - `express`: Enables SSE transport.
  - `zod`: Ensures input validation.
  - `sharp`: Handles image processing.
- **Optional**: **Claude Desktop** (or another MCP client) for enhanced interaction.

---

## Installation

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/yourusername/game-asset-mcp.git
   cd game-asset-mcp
   ```

2. **Install Dependencies**:
   ```bash
   npm install
   ```

3. **Configure Environment**:
   - Copy the example `.env` file:
     ```bash
     cp .env.example .env
     ```
   - Edit `.env` with your **Hugging Face API token** and duplicated **MODEL_SPACE**. See [Configuration](#configuration) for details.

4. **Run the Server**:
   - **Local (stdio transport)**:
     ```bash
     npm start
     ```
   - **Custom Working Directory**:
     ```bash
     node src/index.js /path/to/directory
     ```
   - **Remote (SSE transport)**:
     ```bash
     node src/index.js --sse
     ```
   - **Remote with HTTPS**:
     ```bash
     node src/index.js --sse --https
     ```
     Requires `ssl/key.pem` and `ssl/cert.pem` (see [ssl/README.md](ssl/README.md)).

> **Note**: Uses ES modules (`"type": "module"` in `package.json`). Ensure Node.js 16+ is installed (`node --version`).

---

## Usage

Interact with the server via an **MCP client** (e.g., Claude Desktop) or programmatically:

- **Generate a 2D Asset**:
  - **Command**: `generate_2d_asset prompt:"pixel art sword"`
  - **Output**: Saves a PNG file (e.g., `2d_asset_generate_2d_asset_1698765432.png`) and returns its URI.

- **Generate a 3D Asset**:
  - **Command**: `generate_3d_asset prompt:"isometric 3D castle"`
  - **Output**: Saves OBJ/GLB files and intermediate images, returning their URIs. Provides an operation ID for long-running tasks.

### Prompt Examples
- **Natural Interaction**:
  - `generate_2d_sprite prompt:"pixel art sword"`
  - `generate_3d_model prompt:"isometric 3D castle"`

### With Claude Desktop
After configuring (see [Configuration](#configuration)), type commands directly in the interface.

---

## Configuration

Customize the server via the `.env` file:

### Required Settings
- **HF_TOKEN**: Hugging Face API token.
  ```plaintext
  HF_TOKEN=your_hf_token
  ```
- **MODEL_SPACE**: Your duplicated 3D model space (e.g., `your-username/InstantMesh`).
  - Duplicate one of:
    - [InstantMesh](https://huggingface.co/spaces/tencentARC/InstantMesh)
    - [Hunyuan3D-2](https://huggingface.co/spaces/tencent/Hunyuan3D-2)
    - [Hunyuan3D-2mini-Turbo](https://huggingface.co/spaces/tencent/Hunyuan3D-2mini-Turbo)
  ```plaintext
  MODEL_SPACE=your-username/InstantMesh
  ```

### Optional 3D Model Settings
| Variable                  | Description                                   | Valid Range/Default       |
|---------------------------|-----------------------------------------------|---------------------------|
| `MODEL_3D_STEPS`         | Inference steps                              | Varies by space (see below) |
| `MODEL_3D_GUIDANCE_SCALE`| How closely the model follows the prompt     | 0.0-100.0 (default: 5.0-5.5) |
| `MODEL_3D_OCTREE_RESOLUTION` | Detail level of the 3D model            | Varies by space (see below) |
| `MODEL_3D_SEED`          | Randomness control                          | 0-10000000 (default: varies) |
| `MODEL_3D_REMOVE_BACKGROUND` | Remove image background                | `true`/`false` (default: `true`) |
| `MODEL_3D_TURBO_MODE`    | Generation mode (Hunyuan3D-2mini-Turbo only) | `Turbo`, `Fast`, `Standard` (default: `Turbo`) |
| `MODEL_SPACE_TYPE`       | Override space type detection               | `instantmesh`, `hunyuan3d`, `hunyuan3d_mini_turbo` |

#### Space-Specific Defaults
- **InstantMesh**:
  - Steps: 30-75 (default: 75)
  - Seed: Default 42
- **Hunyuan3D-2**:
  - Steps: 20-50 (default: 20)
  - Guidance Scale: Default 5.5
  - Octree Resolution: `256`, `384`, `512` (default: `256`)
  - Seed: Default 1234
- **Hunyuan3D-2mini-Turbo**:
  - Steps: 1-100 (default: 5 for `Turbo`, 10 for `Fast`, 20 for `Standard`)
  - Guidance Scale: Default 5.0
  - Octree Resolution: 16-512 (default: 256)
  - Seed: Default 1234

### Transport Settings
- **PORT**: SSE transport port (default: 3000).
  ```plaintext
  PORT=3000
  ```

### Claude Desktop Setup
Edit the config file:
- **MacOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
```json
{
  "mcpServers": {
    "game-asset-generator": {
      "command": "node",
      "args": ["/full/path/to/game-asset-mcp/src/index.js"]
    }
  }
}
```
Restart Claude Desktop after editing.

---

## File Management

- **Storage Location**: Assets are saved in `./assets` within the working directory.
- **Naming Convention**: Files use a prefix, tool name, timestamp, and unique ID (e.g., `2d_asset_generate_2d_asset_1698765432_abcd1234.png`).
- **Customization**: Set a custom directory:
  ```bash
  node src/index.js /path/to/custom/directory
  ```
- **Resource Access**: Use MCP URIs (e.g., `asset://2d_asset/filename.png`) to list or read assets.

---

## MCP Integration

The **Model Context Protocol (MCP)** enables this tool to serve AI clients securely:
- **Tools**: `generate_2d_asset`, `generate_3d_asset`.
- **Resources**: Managed via `asset://` URIs.
- **Prompts**: `generate_2d_sprite`, `generate_3d_model`.
- **Compatibility**: Works with **Claude Desktop** and other MCP clients.

---

## Troubleshooting

- **API Errors**: Check network connectivity or rate limits; review `./logs/server.log`.
- **Authentication Issues**: Verify `HF_TOKEN` and `MODEL_SPACE` in `.env`.
- **ES Modules Error**: Ensure Node.js 16+ (`node --version`).
- **Logs**: Inspect detailed logs:
  ```bash
  tail -f ./logs/server.log
  ```

---

## Advanced

### API Endpoints and Integration
- **2D Asset Generation**: Uses `"gokaygokay/Flux-2D-Game-Assets-LoRA"` (50 steps).
- **3D Asset Image Generation**: Uses `"gokaygokay/Flux-Game-Assets-LoRA-v2"` (30 steps).
- **3D Model Conversion**:
  - **InstantMesh**: Multi-step (`/check_input_image`, `/preprocess`, `/generate_mvs`, `/make3d`).
  - **Hunyuan3D-2**: Single-step (`/generation_all`).
  - **Hunyuan3D-2mini-Turbo**: Single-step (`/generation_all`) with turbo modes.

### Versioning
- **Current Version**: 0.3.0 (Added Hunyuan3D-2mini-Turbo support).
- **MCP SDK Version**: 1.7.0.
- **Format**: MAJOR.MINOR.PATCH (SemVer).

### Backend Architecture
- **Core File**: `src/index.js`.
- **Dependencies**: See `package.json`.
- **Security**: Zod validation, path traversal prevention, HTTPS support, rate limiting.
- **Performance**: Async processing, retry with backoff, GPU quota handling.

---

## Contributing

We welcome contributions! To participate:
1. **Fork the Repository**: Create your copy on GitHub.
2. **Make Changes**: Add features, fix bugs, or enhance docs.
3. **Submit a Pull Request**: Detail your changes.
4. **Open Issues**: Report bugs or suggest improvements.

Follow standard coding conventions and include tests where applicable.

---

## License

Licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.
