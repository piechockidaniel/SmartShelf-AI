import { create } from 'zustand';
import type { ShelfBindingDto, ShelfConfigurationDto } from './types';

interface ConfigurationState {
  configuration: ShelfConfigurationDto | null;
  bindings: ShelfBindingDto[];
  dirty: boolean;
  setConfiguration: (value: ShelfConfigurationDto | null) => void;
  setBindings: (value: ShelfBindingDto[]) => void;
}

export const useConfigurationStore = create<ConfigurationState>((set) => ({
  configuration: null,
  bindings: [],
  dirty: false,
  setConfiguration: (configuration) => set({ configuration, bindings: configuration?.bindings ?? [], dirty: false }),
  setBindings: (bindings) => set({ bindings, dirty: true }),
}));
