import { ApiResponse, ErrorApiResponse } from './types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api/v1';

export class ApiClient {
  private static async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<ApiResponse<T>> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    try {
      const response = await fetch(url, { ...options, headers });
      const data = await response.json();

      if (!response.ok) {
        const errorData = data as ErrorApiResponse;
        throw new Error(errorData.title || `Error en la solicitud: ${response.status}`);
      }

      return data as ApiResponse<T>;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Error de conexión con el backend';
      return {
        success: false,
        status: 503,
        detail: message,
      };
    }
  }

  static async getHealth() {
    return this.request<{
      status: string;
      service: string;
      environment: string;
      timestampUtc: string;
      version: string;
    }>('/health');
  }
}

