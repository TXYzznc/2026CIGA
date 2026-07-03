import dotenv from "dotenv";
import path from "path";
import { promises as fs } from "fs";
import { validateNumericRange, validateEnum } from "./validation.js";
import { SPACE_TYPE } from "./spaceTypes.js";

export async function loadConfig() {
  // Allow working directory to be specified via command-line argument
  const workDir = process.argv[2] || process.cwd();
  
  // Debug environment variables
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
  console.error(`ENV: MODEL_SPACE_TYPE = "${process.env.MODEL_SPACE_TYPE || "not set"}"`);
  
  // Verify MODEL_SPACE is set correctly
  console.error(`VERIFICATION: MODEL_SPACE should be set to the value from .env file`);
  console.error(`VERIFICATION: Expected value from .env: "mubarak-alketbi/Hunyuan3D-2mini-Turbo"`);
  console.error(`VERIFICATION: Actual value in process.env: "${process.env.MODEL_SPACE}"`);
  console.error(`VERIFICATION: Is correct? ${process.env.MODEL_SPACE === "mubarak-alketbi/Hunyuan3D-2mini-Turbo"}`);
  console.error("====================================================");
  
  // Create working directory if it doesn't exist
  await fs.mkdir(workDir, { recursive: true });

  // Create a dedicated assets directory
  const assetsDir = path.join(workDir, "assets");
  await fs.mkdir(assetsDir, { recursive: true });
  
  // Validate assetsDir
  if (!assetsDir || typeof assetsDir !== "string") {
    console.error("ERROR: assetsDir is undefined or not a string in config.js");
    throw new Error("Failed to create assets directory");
  }
  
  console.error(`Assets directory: ${assetsDir}`);
  
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
  
  // Port for server
  const port = process.env.PORT || 3000;
  
  // Validate model space type (enum: "instantmesh", "hunyuan3d", "hunyuan3d_mini_turbo")
  const validSpaceTypes = Object.values(SPACE_TYPE);
  const modelSpaceType = validateEnum(
    process.env.MODEL_SPACE_TYPE,
    validSpaceTypes,
    null,
    "MODEL_SPACE_TYPE"
  );

  return {
    workDir,
    assetsDir,
    hfToken,
    modelSpace,
    modelSpaceFromEnv,
    model3dSteps,
    model3dGuidanceScale,
    model3dOctreeResolution,
    model3dSeed,
    model3dRemoveBackground,
    model3dTurboMode,
    validTurboModes,
    port,
    modelSpaceType
  };
}