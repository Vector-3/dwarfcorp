// Snake.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class Snake : Creature, IUpdateableComponent
    {
        public class TailSegment
        {
            public Body Sprite;
            public Vector3 Target;
        }

        public List<TailSegment> Tail;
        
        public Snake()
        {
            
        }

        public Snake(SpriteSheet sprites, Vector3 position, ComponentManager manager, string name, bool hasMeat = true, bool hasBones = true):
            base
            (
                manager,
                new CreatureStats
                {
                    Dexterity = 4,
                    Constitution = 6,
                    Strength = 9,
                    Wisdom = 2,
                    Charisma = 1,
                    Intelligence = 3,
                    Size = 3
                },
                "Carnivore",
                manager.World.PlanService,
                manager.World.Factions.Factions["Carnivore"],
                name
            )
        {
            HasMeat = hasMeat;
            HasBones = hasBones;
            Physics = new Physics
                (
                    manager,
                    name,
                    Matrix.CreateTranslation(position),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    new Vector3(0, 0, 0),
                    1.0f, 1.0f, 0.999f, 0.999f,
                    new Vector3(0, -10, 0)
                );

            Physics.AddChild(this);

            Physics.AddChild(new SelectionCircle(Manager)
            {
                IsVisible = false
            });

            Initialize(sprites);
        }

        public void Initialize(SpriteSheet spriteSheet)
        {
            Physics.Orientation = Physics.OrientMode.Fixed;

            const int frameWidth = 32;
            const int frameHeight = 32;

            var sprite = Physics.AddChild(new CharacterSprite
                (Graphics,
                Manager,
                "snake Sprite",
                Matrix.Identity
                )) as CharacterSprite;

            // Add the idle animation
            sprite.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Forward, spriteSheet, 1, frameWidth, frameHeight, 0, 0);
            sprite.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Left, spriteSheet, 1, frameWidth, frameHeight, 1, 0);
            sprite.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Right, spriteSheet, 1, frameWidth, frameHeight, 2, 0);
            sprite.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Backward, spriteSheet, 1, frameWidth, frameHeight, 3, 0);

            Tail = new List<TailSegment>();
            Physics.AddChild(new Shadow(Manager));
            for (int i = 0; i < 10; ++i)
            {
                var animation = new CharacterSprite(Graphics, Manager, "tail sprite", Matrix.Identity);
                animation.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Forward, spriteSheet, 1, frameWidth, frameHeight, 0, 1);
                animation.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Left, spriteSheet, 1, frameWidth, frameHeight, 1, 1);
                animation.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Right, spriteSheet, 1, frameWidth, frameHeight, 2, 1);
                animation.AddAnimation(CharacterMode.Idle, OrientedAnimation.Orientation.Backward, spriteSheet, 1, frameWidth, frameHeight, 3, 1);
                Tail.Add(
                    new TailSegment()
                    {
                        Sprite = Manager.RootComponent.AddChild(animation) as Body,
                        Target = Physics.LocalTransform.Translation
                    });
                Tail[i].Sprite.AddChild(new Shadow(Manager));
                var inventory = Tail[i].Sprite.AddChild(new Inventory(Manager, "Inventory", Physics.BoundingBox.Extents(), Physics.BoundingBoxPos)) as Inventory;

                if (HasMeat)
                {
                    ResourceLibrary.ResourceType type = Species + " " + ResourceLibrary.ResourceType.Meat;

                    if (!ResourceLibrary.Resources.ContainsKey(type))
                    {
                        ResourceLibrary.Add(new Resource(ResourceLibrary.GetMeat(Species))
                        {
                            Type = type,
                            ShortName = type
                        });
                    }

                    inventory.AddResource(new ResourceAmount(type, 1));
                }

                if (HasBones)
                {
                    ResourceLibrary.ResourceType type = Name + " " + ResourceLibrary.ResourceType.Bones;

                    if (!ResourceLibrary.Resources.ContainsKey(type))
                    {
                        ResourceLibrary.Add(new Resource(ResourceLibrary.Resources[ResourceLibrary.ResourceType.Bones])
                        {
                            Type = type,
                            ShortName = type
                        });
                    }

                    inventory.AddResource(new ResourceAmount(type, 1));
                }
            }

            // Add sensor
            Sensors = Physics.AddChild(new EnemySensor(Manager, "EnemySensor", Matrix.Identity, new Vector3(20, 5, 20), Vector3.Zero)) as EnemySensor;

            // Add AI
            AI = Physics.AddChild(new PacingCreatureAI(Manager, "snake AI", Sensors, PlanService)) as CreatureAI;


            Attacks = new List<Attack>() {new Attack("Bite", 50.0f, 1.0f, 3.0f, SoundSource.Create(ContentPaths.Audio.Oscar.sfx_oc_giant_snake_attack_1), ContentPaths.Effects.bite)};

            Inventory = Physics.AddChild(new Inventory(Manager, "Inventory", Physics.BoundingBox.Extents(), Physics.BoundingBoxPos)) as Inventory;

            Physics.Tags.Add("Snake");
            Physics.Tags.Add("Animal");
            AI.Movement.SetCan(MoveType.ClimbWalls, true);
            AI.Stats.FullName = "Giant Snake";
            AI.Stats.CurrentClass = new EmployeeClass()
            {
                Name = "Giant Snake",
                Levels = new List<EmployeeClass.Level>()
                {
                    new EmployeeClass.Level()
                    {
                        BaseStats = new CreatureStats.StatNums()
                        {
                            Charisma = AI.Stats.Charisma,
                            Constitution = AI.Stats.Constitution,
                            Dexterity = AI.Stats.Dexterity,
                            Intelligence = AI.Stats.Intelligence,
                            Size = AI.Stats.Size,
                            Strength = AI.Stats.Strength,
                            Wisdom = AI.Stats.Wisdom
                        },
                        Name = "Giant Snake",
                        Index = 0
                    },
                  
                }
            };
            AI.Stats.LevelIndex = 0;

            Physics.AddChild(new ParticleTrigger("blood_particle", Manager, "Death Gibs", Matrix.Identity, Vector3.One, Vector3.Zero)
            {
                TriggerOnDeath = true,
                TriggerAmount = 1,
                BoxTriggerTimes = 10,
                SoundToPlay = ContentPaths.Audio.Oscar.sfx_oc_giant_snake_hurt_1,
            });


            NoiseMaker.Noises["Hurt"] = new List<string>() { ContentPaths.Audio.Oscar.sfx_oc_giant_snake_hurt_1 };
            NoiseMaker.Noises["Chirp"] = new List<string>()
            {
                ContentPaths.Audio.Oscar.sfx_oc_giant_snake_neutral_1,
                ContentPaths.Audio.Oscar.sfx_oc_giant_snake_neutral_2
            };

            Physics.AddChild(new Flammable(Manager, "Flames"));
            HasBones = true;
            HasMeat = true;
            Species = "Snake";
        }

        public override void Die()
        {
            foreach (var tail in Tail)
            {
                tail.Sprite.Die();
            }
            base.Die();
        }

        public override void Delete()
        {
            foreach (var tail in Tail)
            {
                tail.Sprite.Delete();
            }
            base.Delete();
        
        }

        public override void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            if ((Physics.Position - Tail.First().Target).LengthSquared() > 0.5f)
            {
                for (int i = Tail.Count  - 1; i > 0; i--)
                {
                    Tail[i].Target = Tail[i - 1].Target;
                }
                Tail[0].Target = Physics.Position;
            }

            int k = 0;
            foreach (var tail in Tail)
            {
                Vector3 diff = Vector3.UnitX;
                if (k == Tail.Count - 1)
                {
                    diff = AI.Position - tail.Sprite.LocalPosition;
                }
                else
                {
                    diff = Tail[k + 1].Sprite.LocalPosition - tail.Sprite.LocalPosition;
                }
                var mat = Matrix.CreateRotationY((float)Math.Atan2(diff.X, -diff.Z));
                mat.Translation = 0.9f*tail.Sprite.LocalPosition + 0.1f*tail.Target;
                tail.Sprite.LocalTransform = mat;
                tail.Sprite.UpdateTransform();
                tail.Sprite.PropogateTransforms();
                k++;
            }
            base.Update(gameTime, chunks, camera);
        }
    }
}
