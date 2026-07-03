import { log } from "./logger.js";

// Space types
export const SPACE_TYPE = {
  INSTANTMESH: "instantmesh",
  HUNYUAN3D: "hunyuan3d",
  HUNYUAN3D_MINI_TURBO: "hunyuan3d_mini_turbo",
  UNKNOWN: "unknown"
};

// Space detection state
export let detectedSpaceType = SPACE_TYPE.UNKNOWN;

// Validate space format
export function validateSpaceFormat(space) {
  if (!space) return false;
  const spaceRegex = /^[a-zA-Z0-9_-]+\/[a-zA-Z0-9_-]+$/;
  return spaceRegex.test(space) && space.split('/').length === 2 && space.split('/')[0].length >= 2 && space.split('/')[1].length >= 2;
}

// Detect space type using API endpoints first, then fallback to name
export async function detectSpaceType(client, modelSpace, workDir) {
  try {
    await log('INFO', `Detecting space type for "${modelSpace}"...`, workDir);

    // Allow manual override via environment variable
    const manualSpaceType = process.env.MODEL_SPACE_TYPE?.toLowerCase();
    if (manualSpaceType) {
      if (Object.values(SPACE_TYPE).includes(manualSpaceType)) {
        detectedSpaceType = manualSpaceType;
        await log('INFO', `Using manually specified space type: ${manualSpaceType}`, workDir);
        return manualSpaceType;
      } else {
        await log('WARN', `Invalid MODEL_SPACE_TYPE "${manualSpaceType}" in .env, proceeding with automatic detection`, workDir);
      }
    }

    await log('INFO', "Fetching API endpoints with view_api()...", workDir);
    const apiInfo = await Promise.race([
      client.view_api(true),
      new Promise((_, reject) => setTimeout(() => reject(new Error("view_api timed out")), 30000))
    ]);

    if (apiInfo && apiInfo.named_endpoints) {
      const endpoints = Object.keys(apiInfo.named_endpoints);
      await log('DEBUG', `Available endpoints: ${endpoints.join(', ')}`, workDir);

      // InstantMesh detection
      if (endpoints.some(e => ['/check_input_image', '/preprocess', '/generate_mvs', '/make3d'].includes(e))) {
        detectedSpaceType = SPACE_TYPE.INSTANTMESH;
        await log('INFO', "Detected InstantMesh based on endpoints", workDir);
        return SPACE_TYPE.INSTANTMESH;
      }

      // Hunyuan3D-2mini-Turbo detection
      if (endpoints.some(e => ['/on_gen_mode_change', '/on_decode_mode_change', '/on_export_click'].includes(e))) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
        await log('INFO', "Detected Hunyuan3D-2mini-Turbo based on endpoints", workDir);
        return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      }

      // Hunyuan3D-2 detection
      if (endpoints.some(e => ['/shape_generation', '/generation_all'].includes(e))) {
        detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
        await log('INFO', "Detected Hunyuan3D-2 based on endpoints", workDir);
        return SPACE_TYPE.HUNYUAN3D;
      }
    }

    // Fallback to space name if endpoint detection fails
    await log('WARN', "Endpoint-based detection failed, falling back to space name", workDir);
    const lowerSpace = modelSpace.toLowerCase();
    if (lowerSpace.includes("hunyuan3d-2mini-turbo") || lowerSpace.includes("hunyuan3d-2mini") || lowerSpace.includes("hunyuan3dmini")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
      await log('INFO', "Detected Hunyuan3D-2mini-Turbo based on name", workDir);
      return SPACE_TYPE.HUNYUAN3D_MINI_TURBO;
    } else if (lowerSpace.includes("hunyuan")) {
      detectedSpaceType = SPACE_TYPE.HUNYUAN3D;
      await log('INFO', "Detected Hunyuan3D-2 based on name", workDir);
      return SPACE_TYPE.HUNYUAN3D;
    } else if (lowerSpace.includes("instantmesh")) {
      detectedSpaceType = SPACE_TYPE.INSTANTMESH;
      await log('INFO', "Detected InstantMesh based on name", workDir);
      return SPACE_TYPE.INSTANTMESH;
    }

    throw new Error(`Unable to determine space type for "${modelSpace}". Please set MODEL_SPACE_TYPE in .env to "instantmesh", "hunyuan3d", or "hunyuan3d_mini_turbo".`);
  } catch (error) {
    await log('ERROR', `Space type detection failed: ${error.message}`, workDir);
    throw error;
  }
}