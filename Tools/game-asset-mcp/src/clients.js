import { Client } from "@gradio/client";
import { InferenceClient } from "@huggingface/inference";
import { log } from "./logger.js";
import { validateSpaceFormat, detectSpaceType } from "./spaceTypes.js";

export async function initializeClients(config) {
  const { hfToken, modelSpace: initialModelSpace, workDir, modelSpaceType } = config;
  let modelSpace = initialModelSpace;

  if (!hfToken) {
    await log('ERROR', "HF_TOKEN is required in the .env file for 2D and 3D asset generation", workDir);
    throw new Error("HF_TOKEN is required in the .env file for 2D and 3D asset generation");
  }

  // Initialize Hugging Face Inference Client
  try {
    await log('INFO', "Initializing Hugging Face Inference Client...", workDir);
    await log('DEBUG', `HF_TOKEN length: ${hfToken ? hfToken.length : 0}`, workDir);
    await log('DEBUG', `HF_TOKEN first 4 chars: ${hfToken ? hfToken.substring(0, 4) : 'none'}`, workDir);
    
    const inferenceClient = new InferenceClient(hfToken);
    await log('INFO', "Successfully initialized Hugging Face Inference Client", workDir);
    await log('DEBUG', "InferenceClient initialized successfully", workDir);
    
    // Connect to Model Space API using Gradio client
    try {
      // Validate model space format
      if (!validateSpaceFormat(modelSpace)) {
        await log('ERROR', `Invalid model space format: "${modelSpace}". Format must be "username/space-name"`, workDir);
        throw new Error(`Invalid model space format: "${modelSpace}". Format must be "username/space-name" (e.g., "your-username/InstantMesh" or "your-username/Hunyuan3D-2"). Please check your MODEL_SPACE environment variable in the .env file.`);
      }
      
      await log('INFO', `Connecting to model space: ${modelSpace}...`, workDir);
      await log('INFO', "Using HF token authentication", workDir);
      
      // Additional logging for debugging
      await log('DEBUG', `MODEL_SPACE environment variable: "${initialModelSpace}"`, workDir);
      await log('DEBUG', `MODEL_SPACE after default fallback: "${modelSpace}"`, workDir);
      await log('DEBUG', `Is MODEL_SPACE using default? ${!initialModelSpace}`, workDir);
      
      // Check if the space exists before trying to connect to it
      await log('DEBUG', "Checking if space exists...", workDir);
      let alternativeSpace = null;
      
      try {
        // Try to fetch the space URL to see if it exists
        const spaceUrl = `https://huggingface.co/spaces/${modelSpace}`;
        await log('DEBUG', `Checking space URL: ${spaceUrl}`, workDir);
        
        const response = await fetch(spaceUrl, {
          method: 'HEAD',
          headers: { Authorization: `Bearer ${hfToken}` }
        });
        
        const spaceExists = response.ok;
        await log('DEBUG', `Space exists check result: ${spaceExists} (status: ${response.status})`, workDir);
        
        if (!spaceExists) {
          // If the space doesn't exist, try alternative casings
          if (modelSpace.toLowerCase().includes("hunyuan3d-2mini") ||
              modelSpace.toLowerCase().includes("hunyuan3dmini")) {
            // Try different casings for Hunyuan3D-2mini-Turbo
            const alternatives = [
              `${modelSpace.split('/')[0]}/Hunyuan3D-2mini-Turbo`,
              `${modelSpace.split('/')[0]}/hunyuan3d-2mini-turbo`,
              `${modelSpace.split('/')[0]}/Hunyuan3D-2mini`,
              `${modelSpace.split('/')[0]}/hunyuan3d-2mini`
            ];
            
            for (const alt of alternatives) {
              const altUrl = `https://huggingface.co/spaces/${alt}`;
              await log('DEBUG', `Checking alternative space URL: ${altUrl}`, workDir);
              
              const altResponse = await fetch(altUrl, {
                method: 'HEAD',
                headers: { Authorization: `Bearer ${hfToken}` }
              });
              
              if (altResponse.ok) {
                alternativeSpace = alt;
                await log('INFO', `Found alternative space: ${alternativeSpace}`, workDir);
                break;
              }
            }
          } else if (modelSpace.toLowerCase().includes("hunyuan")) {
            // Try different casings for Hunyuan3D-2
            const alternatives = [
              `${modelSpace.split('/')[0]}/Hunyuan3D-2`,
              `${modelSpace.split('/')[0]}/hunyuan3d-2`,
              `${modelSpace.split('/')[0]}/HunyuanD-2`
            ];
            
            for (const alt of alternatives) {
              const altUrl = `https://huggingface.co/spaces/${alt}`;
              await log('DEBUG', `Checking alternative space URL: ${altUrl}`, workDir);
              
              const altResponse = await fetch(altUrl, {
                method: 'HEAD',
                headers: { Authorization: `Bearer ${hfToken}` }
              });
              
              if (altResponse.ok) {
                alternativeSpace = alt;
                await log('INFO', `Found alternative space: ${alternativeSpace}`, workDir);
                break;
              }
            }
          } else if (modelSpace.toLowerCase().includes("instantmesh")) {
            // Try different casings for InstantMesh
            const alternatives = [
              `${modelSpace.split('/')[0]}/InstantMesh`,
              `${modelSpace.split('/')[0]}/instantmesh`,
              `${modelSpace.split('/')[0]}/Instantmesh`
            ];
            
            for (const alt of alternatives) {
              const altUrl = `https://huggingface.co/spaces/${alt}`;
              await log('DEBUG', `Checking alternative space URL: ${altUrl}`, workDir);
              
              const altResponse = await fetch(altUrl, {
                method: 'HEAD',
                headers: { Authorization: `Bearer ${hfToken}` }
              });
              
              if (altResponse.ok) {
                alternativeSpace = alt;
                await log('INFO', `Found alternative space: ${alternativeSpace}`, workDir);
                break;
              }
            }
          }
        }
      } catch (error) {
        await log('WARN', `Error checking if space exists: ${error.message}`, workDir);
        // Continue anyway, as the space might still be accessible
      }
      
      // Use the alternative space if found
      if (alternativeSpace) {
        await log('INFO', `Using alternative space: ${alternativeSpace} instead of ${modelSpace}`, workDir);
        await log('DEBUG', `Changing MODEL_SPACE from "${modelSpace}" to "${alternativeSpace}"`, workDir);
        // Store the original value for debugging
        const originalModelSpace = modelSpace;
        modelSpace = alternativeSpace;
        await log('DEBUG', `MODEL_SPACE changed from "${originalModelSpace}" to "${modelSpace}"`, workDir);
      }
      
      // Add a timeout to the connection attempt
      await log('DEBUG', `Creating connection promise for ${modelSpace} with token length ${hfToken.length}`, workDir);
      const connectionPromise = Client.connect(modelSpace, { hf_token: hfToken });
      
      // Create a timeout promise
      const timeoutPromise = new Promise((_, reject) => {
        setTimeout(() => {
          reject(new Error(`Connection to ${modelSpace} timed out after 60 seconds`));
        }, 60000); // 60 second timeout
      });
      
      try {
        // Race the connection promise against the timeout
        await log('DEBUG', "Starting connection attempt with 60 second timeout...", workDir);
        const modelClient = await Promise.race([connectionPromise, timeoutPromise]);
        await log('INFO', `Successfully connected to model space: ${modelSpace}`, workDir);
        
        // Add more diagnostic logs
        await log('DEBUG', "Connection successful, checking client object...", workDir);
        await log('DEBUG', `Client object type: ${typeof modelClient}`, workDir);
        await log('DEBUG', `Client object methods: ${Object.getOwnPropertyNames(Object.getPrototypeOf(modelClient)).join(', ')}`, workDir);
        
        // Detect which space was duplicated
        await log('DEBUG', `Starting space type detection for "${modelSpace}"...`, workDir);
        // Log the modelSpace value right before detection
        await log('DEBUG', `About to detect space type for: "${modelSpace}"`, workDir);
        await log('DEBUG', `modelSpace lowercase: "${modelSpace.toLowerCase()}"`, workDir);
        await log('DEBUG', `Contains "hunyuan3d-2mini-turbo": ${modelSpace.toLowerCase().includes("hunyuan3d-2mini-turbo")}`, workDir);
        await log('DEBUG', `Contains "hunyuan": ${modelSpace.toLowerCase().includes("hunyuan")}`, workDir);
        await log('DEBUG', `Contains "instantmesh": ${modelSpace.toLowerCase().includes("instantmesh")}`, workDir);
        const spaceType = modelSpaceType || await detectSpaceType(modelClient, modelSpace, workDir);
        // We successfully connected to the space, so it's valid
        // Even if we couldn't determine the exact type, we'll use the detected type or manual override
        await log('INFO', `Using space type: ${spaceType}${modelSpaceType ? ' (manually specified)' : ''}`, workDir);
        await log('DEBUG', `Final space type: ${spaceType}`, workDir);
        
        
        return {
          inferenceClient,
          modelClient,
          modelSpace, // Return the potentially updated modelSpace
          spaceType
        };
        
      } catch (error) {
        await log('ERROR', `Error connecting to model space: ${error.message}`, workDir);
        await log('DEBUG', `Error stack: ${error.stack}`, workDir);
        
        if (error.message.includes("timed out")) {
          await log('ERROR', `Connection to model space "${modelSpace}" timed out. This could be due to network issues or the space being unavailable.`, workDir);
          throw new Error(`Connection to model space "${modelSpace}" timed out. Please check your internet connection and try again later. If the problem persists, the space might be unavailable or overloaded.`);
        } else if (error.message.includes("not found") || error.message.includes("404")) {
          await log('ERROR', `Model space "${modelSpace}" not found. Please make sure you've duplicated either InstantMesh or Hunyuan3D-2 space and set the correct space name in your .env file.`, workDir);
          throw new Error(`Model space "${modelSpace}" not found. This could be because: 1. The space doesn't exist - verify you've duplicated it correctly, 2. You've entered the wrong format - it should be "username/space-name" not the full URL, 3. The space is private and your token doesn't have access to it. Please duplicate either space and set the correct name in your .env file.`);
        } else if (error.message.includes("unauthorized") || error.message.includes("401")) {
          await log('ERROR', `Unauthorized access to model space "${modelSpace}". Please check your HF_TOKEN and make sure it has access to this space.`, workDir);
          throw new Error(`Unauthorized access to model space "${modelSpace}". This means your HF_TOKEN doesn't have permission to access this space. Please: 1. Make sure your HF_TOKEN is correct and not expired, 2. Ensure the space is either public or you have granted access to your account, 3. Try generating a new token with appropriate permissions at https://huggingface.co/settings/tokens`);
        } else {
          throw error;
        }
      }
    } catch (error) {
      await log('ERROR', `Error connecting to model space: ${error.message}`, workDir);
      throw new Error(`Failed to connect to model space: ${error.message}`);
    }
  } catch (error) {
    await log('ERROR', `Error initializing Hugging Face Inference Client: ${error.message}`, workDir);
    await log('DEBUG', `Error stack: ${error.stack}`, workDir);
    throw new Error("Failed to initialize Hugging Face Inference Client. Check your HF_TOKEN.");
  }
}