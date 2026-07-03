import { processInstantMesh } from './instantMesh.js';
import { processHunyuan3d } from './hunyuan3d.js';
import { processHunyuan3dMiniTurbo } from './hunyuan3dMiniTurbo.js';
import { SPACE_TYPE } from '../spaceTypes.js';

/**
 * Process 3D asset generation based on the detected space type
 */
export async function process3dAsset({
  spaceType,
  modelClient,
  imageFile,
  imagePath,
  // Removed processedImagePath parameter as it's set internally in the workflows
  prompt,
  operationId,
  toolName,
  assetsDir,
  hfToken,
  modelSpace,
  workDir,
  config,
  retryWithBackoff,
  notifyResourceListChanged
}) {
  // Validate critical parameters
  if (!assetsDir || typeof assetsDir !== "string") {
    throw new Error("assetsDir must be a defined string in process3dAsset");
  }
  
  if (!toolName || typeof toolName !== "string") {
    throw new Error("toolName must be a defined string in process3dAsset");
  }
  
  if (!spaceType) {
    throw new Error("spaceType must be defined in process3dAsset");
  }
  const params = {
    modelClient,
    imageFile,
    imagePath,
    // processedImagePath removed as it's set internally in the workflows
    prompt,
    operationId,
    toolName,
    assetsDir,
    hfToken,
    modelSpace,
    workDir,
    config,
    retryWithBackoff,
    notifyResourceListChanged
  };

  // Call the appropriate workflow based on the space type
  switch (spaceType) {
    case SPACE_TYPE.INSTANTMESH:
      return await processInstantMesh(params);
    
    case SPACE_TYPE.HUNYUAN3D:
      return await processHunyuan3d(params);
    
    case SPACE_TYPE.HUNYUAN3D_MINI_TURBO:
      return await processHunyuan3dMiniTurbo(params);
    
    default:
      throw new Error(`Unsupported space type: ${spaceType}`);
  }
}