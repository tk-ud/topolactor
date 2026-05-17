export type Emission = {
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentIds?: string[];
  data?: Record<string, unknown>;
  errors?: string[];
};

export type DispatchRequest = {
  operationType: string;
  target: string;
  layer: string;
  action: string;
  idOrHubId?: string;
  payload?: Record<string, unknown>;
  context?: Record<string, unknown>;
};

export type DispatchResponse = {
  success: boolean;
  emission?: Emission;
  errors?: string[];
};

/**
 * Sends a DispatchRequest to the backend /api/dispatch endpoint and returns
 * the parsed DispatchResponse.
 *
 * On any fetch-level error (network failure, JSON parse failure) the function
 * returns a failed DispatchResponse carrying the error message rather than
 * throwing, so the caller can treat all outcomes uniformly.
 */
export async function dispatchOperation(
  req: DispatchRequest,
): Promise<DispatchResponse> {
  try {
    const response = await fetch("/api/dispatch", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(req),
    });

    const json: unknown = await response.json();

    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as DispatchResponse;
    }

    return {
      success: false,
      errors: ["dispatch: unexpected response shape from /api/dispatch"],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [message] };
  }
}
