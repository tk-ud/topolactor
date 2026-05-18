/**
 * Structured validation error matching backend ValidationError { Code, Message }.
 * Both PascalCase (from C# serialization) and camelCase are accepted.
 */
export type ValidationError = {
  code?: string;
  Code?: string;
  message?: string;
  Message?: string;
};

/** Extract a display string from a ValidationError regardless of casing. */
export function validationErrorText(e: ValidationError): string {
  const msg = e.message ?? e.Message;
  const code = e.code ?? e.Code;
  if (msg && code) return `[${code}] ${msg}`;
  return msg ?? code ?? "unknown error";
}

export type RecommendationCandidate = {
  value: string;
  score: number;
  probability?: number;
  evidence: string[];
};

export type ContextRouteRecommendation = {
  nextOperations: RecommendationCandidate[];
  nextTokens: RecommendationCandidate[];
  nearestPrefixSessionIds: string[];
  contributingTokens: string[];
  status: "ok" | "insufficient_history" | "explicit_error";
  statusDetail?: string;
};

export type Emission = {
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentIds?: string[];
  data?: Record<string, unknown>;
  errors?: ValidationError[];
  contextRouteRecommendation?: ContextRouteRecommendation;
};

export type DispatchRequest = {
  operationType: string;
  target: string;
  layer: string;
  action: string;
  idOrHubId?: string;
  payload?: Record<string, unknown>;
  context?: Record<string, string>;
};

export type DispatchResponse = {
  success: boolean;
  emission?: Emission;
  errors?: ValidationError[];
};

/**
 * Sends a DispatchRequest to the backend /api/dispatch endpoint and returns
 * the parsed DispatchResponse.
 *
 * If token is provided, adds Authorization: Bearer <token> to the request.
 * On any fetch-level error (network failure, JSON parse failure) the function
 * returns a failed DispatchResponse carrying the error as a ValidationError
 * rather than throwing, so the caller can treat all outcomes uniformly.
 */
export async function dispatchOperation(
  req: DispatchRequest,
  token?: string,
): Promise<DispatchResponse> {
  try {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/dispatch", {
      method: "POST",
      headers,
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
      errors: [{ message: "dispatch: unexpected response shape from /api/dispatch" }],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
