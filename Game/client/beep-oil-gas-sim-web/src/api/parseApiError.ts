interface ValidationProblem {
  title?: string;
  errors?: Record<string, string[]>;
  error?: string;
}

export function parseApiError(status: number, body: string): string {
  if (!body) {
    return status >= 500
      ? 'Server error. Restart the API after pulling latest changes.'
      : `Request failed (${status}).`;
  }

  try {
    const parsed = JSON.parse(body) as ValidationProblem;

    if (parsed.error) {
      return parsed.error;
    }

    if (parsed.errors) {
      const messages = Object.values(parsed.errors).flat();
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }

    if (parsed.title && parsed.title !== 'One or more validation errors occurred.') {
      return parsed.title;
    }
  } catch {
    // plain text or HTML
  }

  if (body.length > 300) {
    return status >= 500
      ? `Server error (${status}). Restart the API after pulling latest changes.`
      : `Request failed (${status}).`;
  }

  return body;
}
