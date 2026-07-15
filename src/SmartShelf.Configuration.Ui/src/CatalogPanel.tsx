import { useEffect, useState } from 'react';
import { api } from './api';
import type { DeviceDto, EvaluationRuleDto, ProductDto } from './types';

export function CatalogPanel({ onChanged }: { onChanged: () => void }) {
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [devices, setDevices] = useState<DeviceDto[]>([]);
  const [rules, setRules] = useState<EvaluationRuleDto[]>([]);
  const [message, setMessage] = useState('');

  const load = async () => {
    const [nextProducts, nextDevices, nextRules] = await Promise.all([api.products(), api.devices(), api.rules()]);
    setProducts(nextProducts); setDevices(nextDevices); setRules(nextRules);
  };
  useEffect(() => { void load(); }, []);

  const addProduct = async () => {
    await api.addProduct({ sku: `SKU-${Date.now()}`, name: 'New product', quantity: 0, expirationDate: new Date(Date.now() + 30 * 86400000).toISOString() });
    setMessage('Product created.'); await load(); onChanged();
  };
  const addDevice = async () => {
    await api.addDevice({ name: 'New sensor', serialNumber: `sensor-${Date.now()}`, kind: 'Sensor' });
    setMessage('Sensor created.'); await load(); onChanged();
  };
  const addRule = async () => {
    await api.addRule({ name: `Low stock ${Date.now()}`, metric: 'InventoryPercent', operator: 'LessThan', threshold: 30, resultStatus: 'Warning', ledColor: 'Yellow', priority: 50 });
    setMessage('Rule created.'); await load(); onChanged();
  };

  return <aside className="catalog-panel">
    <div className="section-heading"><div><h2>Resource catalog</h2><p>Persisted nodes available to every shelf.</p></div></div>
    {message && <div className="toast-inline">{message}</div>}
    <section><header><h3>Products</h3><button onClick={addProduct}>Add</button></header>{products.map(item => <div className="catalog-row" key={item.id}><strong>{item.name}</strong><small>{item.sku} · qty {item.quantity}</small></div>)}</section>
    <section><header><h3>Hardware</h3><button onClick={addDevice}>Add sensor</button></header>{devices.map(item => <div className="catalog-row" key={item.id}><strong>{item.name}</strong><small>{item.kind} · {item.serialNumber}</small></div>)}</section>
    <section><header><h3>Rules</h3><button onClick={addRule}>Add</button></header>{rules.map(item => <div className="catalog-row" key={item.id}><strong>{item.name}</strong><small>{item.metric} {item.operator} {item.threshold}</small></div>)}</section>
  </aside>;
}
