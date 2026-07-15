export interface ShelfDto {
  id: string;
  name: string;
  warehouse: string;
  aisle: string;
  shelfCode: string;
  position: string;
  deviceId: string | null;
  cameraDevice: string | null;
  enabled: boolean;
  createdAt: string;
  updatedAt: string | null;
  version: number;
}

export type ResourceKind =
  | 'Controller'
  | 'Camera'
  | 'Sensor'
  | 'LedOutput'
  | 'Product'
  | 'EvaluationRule';

export interface ResourceNodeDto {
  id: string;
  label: string;
  kind: ResourceKind;
  category: string;
  externalKey: string | null;
}

export interface ShelfBindingDto {
  id: string;
  kind: ResourceKind;
  resourceId: string;
}

export interface ShelfConfigurationDto {
  shelf: ShelfDto;
  bindings: ShelfBindingDto[];
  resources: ResourceNodeDto[];
}

export interface ProductDto { id: string; sku: string; name: string; quantity: number; expirationDate: string; }
export interface DeviceDto { id: string; name: string; serialNumber: string; kind: ResourceKind; status: string; }
export interface EvaluationRuleDto { id: string; name: string; metric: string; operator: string; threshold: number; resultStatus: string; ledColor: string; priority: number; }
