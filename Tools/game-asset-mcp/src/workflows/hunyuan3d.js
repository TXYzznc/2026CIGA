import { promises as fs } from "fs";
import path from "path";
import { log } from "../logger.js";
import { saveFileFromData } from "../utils.js";
import sharp from "sharp";
import crypto from "crypto";

function validateAssetsDir(assetsDir) {
  if (!assetsDir || typeof assetsDir !== "string") {
    throw new Error("assetsDir must be a defined string");
  }
}

export async function processHunyuan3d({
  modelClient,
  imageFile,
  assetsDir,
  imagePath,
  prompt,
  operationId,
  toolName,
  hfToken,
  modelSpace,
  workDir,
  config,
  retryWithBackoff,
  notifyResourceListChanged
}) {
  // Validate assetsDir before proceeding
  validateAssetsDir(assetsDir);
  
  // Debug logging
  await log('DEBUG', `Assets directory: ${assetsDir}`, workDir);
  await log('DEBUG', `Image path: ${imagePath}`, workDir);
  await log('DEBUG', `Tool name: ${toolName}`, workDir);

  const { model3dSteps, model3dGuidanceScale, model3dSeed, model3dOctreeResolution, model3dRemoveBackground } = config;

  await log('INFO', "Processing with Hunyuan3D-2 space", workDir);

  // Convert image to PNG
  const pngBuffer = await sharp(imageFile).png().toBuffer();
  const mimeType = 'image/png';
  const imageFilename = `input_${Date.now()}_${crypto.randomBytes(4).toString("hex")}.png`;
  // Validate path construction
  if (!toolName) {
    throw new Error("toolName must be defined");
  }
  const pngImagePath = path.join(assetsDir, `3d_processed_${toolName}_${Date.now()}.png`);
  await fs.writeFile(pngImagePath, pngBuffer);
  await log('INFO', `Converted image saved at: ${pngImagePath}`, workDir);
  await notifyResourceListChanged();

  // Set parameters with defaults
  const steps = Math.max(20, Math.min(50, model3dSteps || 20));
  const guidanceScale = model3dGuidanceScale || 5.5;
  const seed = model3dSeed || 1234;
  const validOctreeResolutions = ["256", "384", "512"];
  const octreeResolution = validOctreeResolutions.includes(model3dOctreeResolution) ? model3dOctreeResolution : "256";
  const removeBackground = model3dRemoveBackground !== false;

  await log('INFO', `Parameters: steps=${steps}, guidance_scale=${guidanceScale}, seed=${seed}, octree_resolution=${octreeResolution}, remove_background=${removeBackground}`, workDir);

  const modelResult = await retryWithBackoff(async () => {
    return await modelClient.predict("/generation_all", [
      prompt,
      new File([pngBuffer], imageFilename, { type: mimeType }),
      steps,
      guidanceScale,
      seed,
      octreeResolution,
      removeBackground
    ]);
  }, operationId, 5);

  if (!modelResult || !modelResult.data || modelResult.data.length < 2) {
    throw new Error("3D model generation failed: insufficient data in response");
  }

  // Extract URLs (Hunyuan3D-2 uses result.data[index].url)
  let texturedMeshUrl = modelResult.data[1]?.url;
  let whiteMeshUrl = modelResult.data[0]?.url;

  if (!texturedMeshUrl) {
    await log('WARN', "Textured mesh not found, falling back to white mesh", workDir);
    if (!whiteMeshUrl) throw new Error("No valid mesh found in response");
    texturedMeshUrl = whiteMeshUrl;
  }

  const headers = { Authorization: `Bearer ${hfToken}` };
  const response = await fetch(texturedMeshUrl, { headers });
  if (!response.ok) throw new Error(`Failed to fetch mesh: ${response.status} ${response.statusText}`);
  const buffer = await response.arrayBuffer();

  // Save as both OBJ and GLB (Hunyuan3D-2 outputs GLB, but we'll alias for consistency)
  // Log the parameters being passed to saveFileFromData for debugging
  await log('DEBUG', `Saving GLB with parameters: toolName=${toolName}, assetsDir=${assetsDir}`, workDir);
  const glbResult = await saveFileFromData(buffer, "3d_model", "glb", toolName, assetsDir, hfToken, modelSpace, workDir);
  await log('INFO', `GLB model saved at: ${glbResult.filePath}`, workDir);
  await notifyResourceListChanged();

  const objResult = glbResult; // Alias for consistency, as Hunyuan3D-2 doesn't provide separate OBJ

  return { objResult, glbResult };
}