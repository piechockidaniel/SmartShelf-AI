import type { ResourceKind, ShelfBindingDto } from './types';

export const singletonKinds = new Set<ResourceKind>(['Controller', 'Camera', 'LedOutput']);

export function connectResource(
  bindings: ShelfBindingDto[],
  resourceId: string,
  kind: ResourceKind,
  id: string = crypto.randomUUID(),
): ShelfBindingDto[] {
  if (bindings.some((binding) => binding.kind === kind && binding.resourceId === resourceId)) return bindings;
  const retained = singletonKinds.has(kind)
    ? bindings.filter((binding) => binding.kind !== kind)
    : bindings;
  return [...retained, { id, kind, resourceId }];
}

export function removeBinding(bindings: ShelfBindingDto[], id: string): ShelfBindingDto[] {
  return bindings.filter((binding) => binding.id !== id);
}
