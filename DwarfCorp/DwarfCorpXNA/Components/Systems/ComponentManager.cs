using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using Newtonsoft.Json;

namespace DwarfCorp
{
    /// <summary>
    /// Handles components. All game objects (dwarves, trees, lamps, ravenous wolverines) are just a 
    /// collection of components. Together, the collection is called an 'entity'. Components form a 
    /// tree. Each component has a parent and 0 to N children.
    /// </summary>
    public class ComponentManager
    {
        public class ComponentSaveData
        {
            public List<GameComponent> SaveableComponents;
            public uint RootComponent;
        }

        private Dictionary<uint, GameComponent> Components;

        private Dictionary<System.Type, List<IUpdateableComponent>> UpdateableComponents =
            new Dictionary<Type, List<IUpdateableComponent>>();
        private List<IRenderableComponent> Renderables = new List<IRenderableComponent>();
        private List<MinimapIcon> MinimapIcons = new List<MinimapIcon>();
        private List<GameComponent> Removals = new List<GameComponent>();
        private List<GameComponent> Additions = new List<GameComponent>();

        public Body RootComponent { get; private set; }

        public void SetRootComponent(Body Component)
        {
            Component.World = World;
            RootComponent = Component;
        }

        private Mutex AdditionMutex = new Mutex();
        private Mutex RemovalMutex = new Mutex();

        public IEnumerable<IRenderableComponent> GetRenderables() { return Renderables; }
        public IEnumerable<MinimapIcon> GetMinimapIcons() { return MinimapIcons; }

        public WorldManager World { get; set; }

        public ComponentSaveData GetSaveData()
        {
            // Just in case the root was tagged unserializable for whatever reason.
            RootComponent.SetFlag(GameComponent.Flag.ShouldSerialize, true);

            foreach (var component in Components)
                component.Value.PrepareForSerialization();

            var serializableComponents = Components.Where(c => c.Value.IsFlagSet(GameComponent.Flag.ShouldSerialize)).Select(c => c.Value).ToList();

            return new ComponentSaveData
            {
                SaveableComponents = serializableComponents,
                RootComponent = RootComponent.GlobalID
            };
        }

        public void CleanupSaveData()
        {
            foreach (var component in Components)
                component.Value.SerializableChildren = null;
        }

        public ComponentManager(ComponentSaveData SaveData, WorldManager World)
        {
            Components = new Dictionary<uint, GameComponent>();
            foreach (var component in SaveData.SaveableComponents)
                Components.Add(component.GlobalID, component);
            RootComponent = Components[SaveData.RootComponent] as Body;

            this.World = World;

            foreach (var component in Components)
            {
                if (component.Value is IUpdateableComponent)
                {
                    var type = component.Value.GetType();
                    if (!UpdateableComponents.ContainsKey(type))
                        UpdateableComponents.Add(type, new List<IUpdateableComponent>());
                    UpdateableComponents[type].Add(component.Value as IUpdateableComponent);
                }

                if (component.Value is IRenderableComponent)
                    Renderables.Add(component.Value as IRenderableComponent);

                if (component.Value is MinimapIcon)
                    MinimapIcons.Add(component.Value as MinimapIcon);
            }
       
            foreach (var component in SaveData.SaveableComponents)
                component.PostSerialization();
        }

        public ComponentManager(WorldManager state, CompanyInformation CompanyInformation, List<Faction> natives)
        {
            World = state;
            Components = new Dictionary<uint, GameComponent>();
        }

        public List<Body> SelectRootBodiesOnScreen(Rectangle selectionRectangle, Camera camera)
        {
            if (World.SelectionBuffer == null)
                return new List<Body>();

            HashSet<Body> toReturn = new HashSet<Body>(); // Hashset ensures all bodies are unique.
            foreach (uint id in World.SelectionBuffer.GetIDsSelected(selectionRectangle))
            {
                GameComponent component;
                if (!Components.TryGetValue(id, out component))
                    continue;

                if (!component.IsVisible) continue; // Then why was it drawn in the selection buffer??
                var toAdd = component.GetEntityRootComponent().GetComponent<Body>();
                if (!toReturn.Contains(toAdd))
                    toReturn.Add(component.GetEntityRootComponent().GetComponent<Body>());
            }
            return toReturn.ToList();
        }

        public void AddComponent(GameComponent component)
        {
            AdditionMutex.WaitOne();
            Additions.Add(component);
            AdditionMutex.ReleaseMutex();
        }

        public void RemoveComponent(GameComponent component)
        {
            RemovalMutex.WaitOne();
            Removals.Add(component);
            RemovalMutex.ReleaseMutex();
        }

        private void RemoveComponentImmediate(GameComponent component)
        {
            if (!Components.ContainsKey(component.GlobalID))
                return;
            
            Components.Remove(component.GlobalID);

            if (component is IUpdateableComponent)
            {
                var type = component.GetType();
                if (UpdateableComponents.ContainsKey(type))
                    UpdateableComponents[type].Remove(component as IUpdateableComponent);
            }

            if (component is IRenderableComponent)
                Renderables.Remove(component as IRenderableComponent);

            if (component is MinimapIcon)
                MinimapIcons.Remove(component as MinimapIcon);

            foreach (var child in new List<GameComponent>(component.Children))
                RemoveComponentImmediate(child);
        }

        private void AddComponentImmediate(GameComponent component)
        {
            if (Components.ContainsKey(component.GlobalID))
            {
                if (Object.ReferenceEquals(Components[component.GlobalID], component)) return;
                throw new InvalidOperationException("Attempted to add component with same ID as existing component.");
            }

            Components[component.GlobalID] = component;

            if (component is IUpdateableComponent)
            {
                var type = component.GetType();
                if (!UpdateableComponents.ContainsKey(type))
                    UpdateableComponents.Add(type, new List<IUpdateableComponent>());
                UpdateableComponents[type].Add(component as IUpdateableComponent);
            }

            if (component is IRenderableComponent)
                Renderables.Add(component as IRenderableComponent);

            if (component is MinimapIcon)
                MinimapIcons.Add(component as MinimapIcon);
        }

        public void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            if (RootComponent != null)
                RootComponent.UpdateTransformsRecursive(null);

            foreach (var componentType in UpdateableComponents)
                foreach (var component in componentType.Value)
                    if (component.IsActive)
                        component.Update(gameTime, chunks, camera);
            
            AdditionMutex.WaitOne();
            foreach (GameComponent component in Additions)
                AddComponentImmediate(component);

            Additions.Clear();
            AdditionMutex.ReleaseMutex();

            RemovalMutex.WaitOne();
            foreach (GameComponent component in Removals)
                RemoveComponentImmediate(component);

            Removals.Clear();
            RemovalMutex.ReleaseMutex();
        }

        public uint GetMaxComponentID()
        {
            return Components.Aggregate<KeyValuePair<uint, GameComponent>, uint>(0, (current, component) => Math.Max(current, component.Value.GlobalID));
        }
    }

}
