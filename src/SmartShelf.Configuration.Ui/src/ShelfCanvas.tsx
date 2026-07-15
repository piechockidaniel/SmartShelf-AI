import { useCallback, useEffect, useMemo } from 'react';
import {
  Background,
  BackgroundVariant,
  Controls,
  Handle,
  MiniMap,
  Position,
  ReactFlow,
  addEdge,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { connectResource, removeBinding } from './bindings';
import { useConfigurationStore } from './store';
import type { ResourceKind } from './types';

const kindColors: Record<ResourceKind, string> = {
  Controller: '#38bdf8', Camera: '#a78bfa', Sensor: '#22d3ee', LedOutput: '#f59e0b',
  Product: '#34d399', EvaluationRule: '#fb7185',
};

function ShelfNode({ data }: { data: { label: string } }) {
  return <div className="graph-node shelf-node"><strong>{data.label}</strong><small>Shelf aggregate</small><Handle type="source" position={Position.Right} /></div>;
}

function ResourceNode({ data }: { data: { label: string; kind: ResourceKind; externalKey?: string | null } }) {
  return <div className="graph-node resource-node" style={{ borderColor: kindColors[data.kind] }}>
    <Handle type="target" position={Position.Left} style={{ background: kindColors[data.kind] }} />
    <strong>{data.label}</strong><small>{data.kind}{data.externalKey ? ` · ${data.externalKey}` : ''}</small>
  </div>;
}

const nodeTypes = { shelf: ShelfNode, resource: ResourceNode };

export function ShelfCanvas() {
  const { configuration, bindings, setBindings } = useConfigurationStore();
  const graphNodes = useMemo<Node[]>(() => {
    if (!configuration) return [];
    const nodes: Node[] = [{ id: `shelf-${configuration.shelf.id}`, type: 'shelf', position: { x: 40, y: 80 }, data: { label: configuration.shelf.name }, draggable: false }];
    let y = 20;
    for (const resource of configuration.resources) {
      nodes.push({
        id: `resource-${resource.id}`, type: 'resource', position: { x: 520, y },
        data: { label: resource.label, kind: resource.kind, externalKey: resource.externalKey },
        draggable: false,
      });
      y += 82;
    }
    return nodes;
  }, [configuration]);
  const graphEdges = useMemo<Edge[]>(() => bindings.map((binding) => ({
    id: binding.id,
    source: `shelf-${configuration?.shelf.id}`,
    target: `resource-${binding.resourceId}`,
    style: { stroke: kindColors[binding.kind], strokeWidth: 2 },
    data: { binding },
  })), [bindings, configuration]);
  const [nodes, setNodes, onNodesChange] = useNodesState(graphNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(graphEdges);
  useEffect(() => setNodes(graphNodes), [graphNodes, setNodes]);
  useEffect(() => setEdges(graphEdges), [graphEdges, setEdges]);

  const onConnect = useCallback((connection: Connection) => {
    if (!configuration || !connection.target) return;
    const resourceId = connection.target.replace('resource-', '');
    const resource = configuration.resources.find((item) => item.id === resourceId);
    if (!resource) return;
    const nextBindings = connectResource(bindings, resourceId, resource.kind);
    setBindings(nextBindings);
    setEdges(addEdge({
      id: nextBindings.at(-1)!.id, source: connection.source!, target: connection.target,
      style: { stroke: kindColors[resource.kind], strokeWidth: 2 },
    }, edges));
  }, [bindings, configuration, edges, setBindings, setEdges]);

  return <ReactFlow
    nodes={nodes} edges={edges} nodeTypes={nodeTypes}
    onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
    onEdgeDoubleClick={(_, edge) => setBindings(removeBinding(bindings, edge.id))}
    fitView proOptions={{ hideAttribution: true }}>
    <Background variant={BackgroundVariant.Dots} gap={20} color="#334155" />
    <Controls /><MiniMap pannable zoomable />
  </ReactFlow>;
}
