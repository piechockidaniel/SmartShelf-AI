import { describe, expect, it } from 'vitest';
import { connectResource, removeBinding } from './bindings';

describe('shelf bindings', () => {
  it('replaces singleton hardware and keeps many products', () => {
    let bindings = connectResource([], 'controller-1', 'Controller', 'b1');
    bindings = connectResource(bindings, 'controller-2', 'Controller', 'b2');
    bindings = connectResource(bindings, 'product-1', 'Product', 'b3');
    bindings = connectResource(bindings, 'product-2', 'Product', 'b4');
    expect(bindings.map((binding) => binding.id)).toEqual(['b2', 'b3', 'b4']);
    expect(removeBinding(bindings, 'b3')).toHaveLength(2);
  });
});
