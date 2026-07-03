// Function to validate numeric value within a range
export function validateNumericRange(value, min, max, defaultValue, paramName) {
  if (value === null || value === undefined) {
    return defaultValue;
  }
  
  const numValue = Number(value);
  if (isNaN(numValue)) {
    console.error(`Invalid ${paramName} value: "${value}" is not a number. Using default: ${defaultValue}`);
    return defaultValue;
  }
  
  if (numValue < min) {
    console.error(`${paramName} value ${numValue} is below minimum (${min}). Using minimum value.`);
    return min;
  }
  
  if (numValue > max) {
    console.error(`${paramName} value ${numValue} exceeds maximum (${max}). Using maximum value.`);
    return max;
  }
  
  return numValue;
}

// Function to validate enum values
export function validateEnum(value, allowedValues, defaultValue, paramName) {
  if (value === null || value === undefined) {
    return defaultValue;
  }
  
  if (!allowedValues.includes(value)) {
    console.error(`Invalid ${paramName} value: "${value}". Allowed values: [${allowedValues.join(', ')}]. Using default: ${defaultValue}`);
    return defaultValue;
  }
  
  return value;
}

// MCP Error Codes
export const MCP_ERROR_CODES = {
  InvalidRequest: -32600,
  MethodNotFound: -32601,
  InvalidParams: -32602,
  InternalError: -32603,
  ParseError: -32700
};