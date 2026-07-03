import { promises as fs } from "fs";
import path from "path";
import { log } from "../logger.js";
import { saveFileFromData } from "../utils.js";
import sharp from "sharp";
import crypto from "crypto";

/**
 * Workflow for InstantMesh space
 */
export async function processInstantMesh({
  modelClient,
  imageFile,
  imagePath,
  // Removed processedImagePath as it's set internally
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
  const {
    model3dSteps,
    model3dSeed,
    model3dRemoveBackground
    // Note: model3dTurboMode is not used in InstantMesh space
    // It's only applicable to Hunyuan3D-2mini-Turbo space
  } = config;
  
  // Convert the original image to PNG to ensure format consistency
  await log('INFO', "Converting image to PNG for API compatibility", workDir);
  const pngBuffer = await sharp(imageFile).png().toBuffer();
  const mimeType = 'image/png';
  const imageFilename = `input_${Date.now()}_${crypto.randomBytes(4).toString("hex")}.png`;
  
  // 2.1: Check if the image is valid
  await log('DEBUG', "Validating image for 3D conversion with InstantMesh...", workDir);
  await log('INFO', "Using InstantMesh space", workDir);
  
  const checkResult = await retryWithBackoff(async () => {
    return await modelClient.predict("/check_input_image", [
      new File([pngBuffer], imageFilename, { type: mimeType })
    ]);
  }, operationId);
  
  // 2.2: Preprocess the image (with background removal)
  await log('DEBUG', "Preprocessing image with InstantMesh...", workDir);
  const preprocessResult = await retryWithBackoff(async () => {
    return await modelClient.predict("/preprocess", [
      new File([pngBuffer], imageFilename, { type: mimeType }),
      model3dRemoveBackground // Use configured value
    ]);
  }, operationId);
  
  if (!preprocessResult || !preprocessResult.data) {
    throw new Error("Image preprocessing failed");
  }
  
  await log('DEBUG', "Successfully preprocessed image with InstantMesh", workDir);
  
  // Save the preprocessed image
  const processedResult = await saveFileFromData(
    preprocessResult.data,
    "3d_processed",
    "png",
    toolName,
    assetsDir,
    hfToken,
    modelSpace,
    workDir
  );
  const processedImagePath = processedResult.filePath;
  await log('INFO', `Preprocessed image saved at: ${processedImagePath}`, workDir);
  
  // Notify clients that a new resource is available
  await notifyResourceListChanged();
  
  // 2.3: Generate multi-views
  await log('DEBUG', "Generating multi-views with InstantMesh...", workDir);
  const processedImageFile = await fs.readFile(processedImagePath);
  
  // Use configured values or defaults for InstantMesh with validation
  // InstantMesh steps range: 30-75
  let steps = model3dSteps !== null ? model3dSteps : 75; // Default: 75
  steps = Math.max(30, Math.min(75, steps));
  
  // Any integer is valid for seed, but use default if not provided
  const seed = model3dSeed !== null ? model3dSeed : 42; // Default: 42
  
  await log('INFO', `InstantMesh parameters - steps: ${steps}, seed: ${seed}`, workDir);
  
  const mvsResult = await retryWithBackoff(async () => {
    return await modelClient.predict("/generate_mvs", [
      new File([processedImageFile], path.basename(processedImagePath), { type: "image/png" }),
      steps,
      seed
    ]);
  }, operationId);
  
  if (!mvsResult || !mvsResult.data) {
    throw new Error("Multi-view generation failed");
  }
  
  await log('DEBUG', "Successfully generated multi-view image with InstantMesh", workDir);
  
  // Save the multi-view image
  const mvsResult2 = await saveFileFromData(
    mvsResult.data,
    "3d_multiview",
    "png",
    toolName,
    assetsDir,
    hfToken,
    modelSpace,
    workDir
  );
  const mvsImagePath = mvsResult2.filePath;
  await log('INFO', `Multi-view image saved at: ${mvsImagePath}`, workDir);
  
  // Notify clients that a new resource is available
  await notifyResourceListChanged();
  
  // 2.4: Generate 3D models (OBJ and GLB)
  await log('DEBUG', "Generating 3D models with InstantMesh...", workDir);
  
  // This step is particularly prone to GPU quota errors, so use retry with backoff
  const modelResult = await retryWithBackoff(async () => {
    return await modelClient.predict("/make3d", []);
  }, operationId, 5); // Pass operationId and more retries for this critical step
  
  if (!modelResult || !modelResult.data || !modelResult.data.length) {
    throw new Error("3D model generation failed");
  }
  
  await log('DEBUG', "Successfully generated 3D models with InstantMesh", workDir);
  
  // Save debug information for troubleshooting
  const modelDebugFilename = path.join(assetsDir, `model_data_${Date.now()}.json`);
  await fs.writeFile(modelDebugFilename, JSON.stringify(modelResult, null, 2));
  await log('DEBUG', `Model data saved as JSON at: ${modelDebugFilename}`, workDir);
  
  // InstantMesh returns both OBJ and GLB formats
  const objModelData = modelResult.data[0];
  const glbModelData = modelResult.data[1];
  await log('DEBUG', `InstantMesh: Using modelResult.data[0] for OBJ and modelResult.data[1] for GLB`, workDir);
  
  // Save both model formats and notify clients of resource changes
  const objResult = await saveFileFromData(
    objModelData, 
    "3d_model", 
    "obj", 
    toolName, 
    assetsDir, 
    hfToken, 
    modelSpace, 
    workDir
  );
  await log('INFO', `OBJ model saved at: ${objResult.filePath}`, workDir);
  
  // Notify clients that a new resource is available
  await notifyResourceListChanged();
  
  const glbResult = await saveFileFromData(
    glbModelData, 
    "3d_model", 
    "glb", 
    toolName, 
    assetsDir, 
    hfToken, 
    modelSpace, 
    workDir
  );
  await log('INFO', `GLB model saved at: ${glbResult.filePath}`, workDir);
  
  // Notify clients that a new resource is available
  await notifyResourceListChanged();
  
  return {
    objResult,
    glbResult
  };
}