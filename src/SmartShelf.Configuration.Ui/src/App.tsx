import { useEffect, useState } from 'react';
import { ReactFlowProvider } from '@xyflow/react';
import { api } from './api';
import { configurationSaveErrorMessage } from './errors';
import { CatalogPanel } from './CatalogPanel';
import { ShelfCanvas } from './ShelfCanvas';
import { useConfigurationStore } from './store';
import type { ShelfDto } from './types';

export default function App() {
  const [shelves, setShelves] = useState<ShelfDto[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [view, setView] = useState<'map' | 'catalog'>('map');
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  const { configuration, bindings, dirty, setConfiguration } = useConfigurationStore();

  const loadShelves = async () => {
    const items = await api.shelves();
    setShelves(items);
    if (!selectedId && items.length) setSelectedId(items[0].id);
  };
  useEffect(() => { void loadShelves().catch(error => setMessage(String(error))); }, []);
  useEffect(() => {
    if (!selectedId) { setConfiguration(null); return; }
    void api.configuration(selectedId).then(setConfiguration).catch(error => setMessage(String(error)));
  }, [selectedId, setConfiguration]);

  const createShelf = async () => {
    const created = await api.createShelf({
      name: `Shelf ${shelves.length + 1}`,
      location: { warehouse: 'WH-1', aisle: 'A1', shelf: `S${shelves.length + 1}`, position: 'P1' },
    });
    await loadShelves(); setSelectedId(created.id); setView('map');
  };

  const save = async () => {
    if (!configuration) return;
    setSaving(true); setMessage('');
    try {
      const updated = await api.saveConfiguration(configuration.shelf.id, configuration.shelf.version, bindings);
      setConfiguration(updated); setMessage(`Saved version ${updated.shelf.version}.`); await loadShelves();
    } catch (error) {
      setMessage(configurationSaveErrorMessage(error));
    } finally { setSaving(false); }
  };

  return <div className="app-shell">
    <header className="topbar">
      <div><span className="eyebrow">SmartShelf AI</span><h1>Configuration studio</h1></div>
      <nav><button className={view === 'map' ? 'active' : ''} onClick={() => setView('map')}>Shelf map</button><button className={view === 'catalog' ? 'active' : ''} onClick={() => setView('catalog')}>Catalog</button></nav>
      <div className="toolbar-status">{configuration && <span>v{configuration.shelf.version}</span>}{dirty && <span className="dirty">Unsaved</span>}<button className="save" onClick={save} disabled={!dirty || saving}>{saving ? 'Saving…' : 'Save'}</button></div>
    </header>
    {message && <div className="message" role="status">{message}<button onClick={() => selectedId && api.configuration(selectedId).then(setConfiguration)}>Reload</button></div>}
    <div className="workspace">
      <aside className="shelf-sidebar">
        <div className="section-heading"><div><h2>Shelves</h2><p>{shelves.length} configured</p></div><button onClick={createShelf}>New</button></div>
        {shelves.map(shelf => <button key={shelf.id} className={`shelf-item ${selectedId === shelf.id ? 'selected' : ''}`} onClick={() => { setSelectedId(shelf.id); setView('map'); }}>
          <strong>{shelf.name}</strong><small>{shelf.warehouse} / {shelf.aisle} / {shelf.shelfCode}</small><span>{shelf.enabled ? 'Enabled' : 'Disabled'} · v{shelf.version}</span>
        </button>)}
      </aside>
      <main className="main-panel">
        {view === 'catalog' ? <CatalogPanel onChanged={() => selectedId && api.configuration(selectedId).then(setConfiguration)} /> : configuration ? <>
          <div className="canvas-heading"><div><h2>{configuration.shelf.name}</h2><p>Drag from the shelf handle to a resource. Double-click a connection to remove it.</p></div><span>{bindings.length} connections</span></div>
          <div className="canvas"><ReactFlowProvider><ShelfCanvas /></ReactFlowProvider></div>
        </> : <div className="empty">Create or select a shelf to configure it.</div>}
      </main>
    </div>
  </div>;
}
