import type {
  DeviceDto,
  EvaluationRuleDto,
  ProductDto,
  ResourceNodeDto,
  ShelfBindingDto,
  ShelfConfigurationDto,
  ShelfDto,
} from './types';

const root = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'http://localhost:5247/api/v1';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${root}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  });
  if (!response.ok) {
    const details = await response.text();
    const error = new Error(details || `API returned ${response.status}`) as Error & { status?: number };
    error.status = response.status;
    throw error;
  }
  return response.status === 204 ? (undefined as T) : response.json() as Promise<T>;
}

export const api = {
  shelves: () => request<ShelfDto[]>('/shelves'),
  createShelf: (body: object) => request<ShelfDto>('/shelves', { method: 'POST', body: JSON.stringify(body) }),
  configuration: (id: string) => request<ShelfConfigurationDto>(`/shelves/${id}/configuration`),
  saveConfiguration: (id: string, expectedVersion: number, bindings: ShelfBindingDto[]) =>
    request<ShelfConfigurationDto>(`/shelves/${id}/configuration`, {
      method: 'PUT',
      body: JSON.stringify({ expectedVersion, bindings: bindings.map(({ kind, resourceId }) => ({ kind, resourceId })) }),
    }),
  schema: () => request<ResourceNodeDto[]>('/shelf-resource-schema'),
  products: () => request<ProductDto[]>('/products'),
  devices: () => request<DeviceDto[]>('/devices'),
  rules: () => request<EvaluationRuleDto[]>('/evaluation-rules'),
  addProduct: (body: object) => request<ProductDto>('/products', { method: 'POST', body: JSON.stringify(body) }),
  addDevice: (body: object) => request<DeviceDto>('/devices', { method: 'POST', body: JSON.stringify(body) }),
  addRule: (body: object) => request<EvaluationRuleDto>('/evaluation-rules', { method: 'POST', body: JSON.stringify(body) }),
};
