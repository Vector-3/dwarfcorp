using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp.Gui.Widgets
{
    public class ContextMenu : Widget
    {
        public List<ContextCommands.ContextCommand> Commands;
        public Body Body;
        public List<Body> MultiBody;

        public WorldManager World;
        public int Width;
        public override void Construct()
        {
            var text = Body != null ? DwarfSelectorTool.GetMouseOverText(new List<Body>() { Body }) : "Selected Objects";
            var font = Root.GetTileSheet("font10");
            var size = font.MeasureString(text).Scale(TextSize);
            Width = Math.Max(size.X + 32, 128);
            Rect = new Rectangle(0, 0, Width, Commands.Count * 16 + 32);
            MaximumSize = new Point(Width, Commands.Count * 16 + 32);
            MaximumSize = new Point(Width, Commands.Count * 16 + 32);
            Border = "border-dark";
            TextColor = Color.White.ToVector4();
            Root.RegisterForUpdate(this);
            base.Construct();

            AddChild(new Gui.Widget()
            {
                Font = "font10",
                MinimumSize = new Point(128, 16),
                AutoLayout = AutoLayout.DockTop,
                Text = text
            });

            foreach (var command in Commands)
            {
                var iconSheet = Root.GetTileSheet(command.Icon.Sheet);
                var lambdaCommand = command;
                AddChild(new Gui.Widget()
                {
                    AutoLayout = AutoLayout.DockTop,
                    MinimumSize = new Point(Width, 16),
                    Text = command.Name,
                    OnClick = (sender, args) =>
                    {
                        if (MultiBody != null && MultiBody.Count > 0)
                        {
                            foreach (var body in MultiBody.Where(body => !body.IsDead && lambdaCommand.CanBeAppliedTo(body, body.World)))
                            {
                                lambdaCommand.Apply(body, World);
                            }
                        }
                        else
                        {
                            lambdaCommand.Apply(Body, World);
                        }
                        sender.Parent.Close();
                    },
                    ChangeColorOnHover = true,
                    HoverTextColor = Color.DarkRed.ToVector4()
                });
            }

            OnUpdate += (sender, time) =>
            {
                if (Body == null)
                {
                    return;
                }

                if (Body.IsDead)
                {
                    this.Close();
                    return;
                }

                var menuCenter = World.Camera.Project(Body.Position);
                Rect = new Rectangle((int)menuCenter.X, (int)menuCenter.Y, Width, Commands.Count * 16 + 32);
                Layout();
                Invalidate();
            };

        }
    }
}
