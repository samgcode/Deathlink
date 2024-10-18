using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.CelesteNet.Client;
using MDraw = Monocle.Draw;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System;
using Celeste.Mod.Deathlink;

namespace Celeste.Mod.Deathlink.Message
{
  public class StatusComponent : DrawableGameComponent
  {

    private Dictionary<MessageType, Icon> icons;

    private List<Message> messages;

    // private string text;
    private const float timeIn = 0.3f;
    private const float timeOut = 0.2f;

    // private float timeText;
    private const float timeTextMax = 100f;


    public StatusComponent(Game game) : base(game)
    {
      UpdateOrder = 10000;
      DrawOrder = 10200;

      Enabled = true;

      this.messages = new();
    }

    protected override void LoadContent()
    {
      base.LoadContent();

      icons = new() {
        { MessageType.Message, new(GFX.Gui["reloader/cogwheel"], 0.25f) },
        { MessageType.Death, new(GFX.Gui["collectables/skullRed"], 1f) },
        { MessageType.Error, new(GFX.Gui["emoji/celestenet_warning"], 1f) },
      };

      // foreach (KeyValuePair<string, MTexture> kvp in GFX.Gui.textures)
      // {
      //   Logger.Log(LogLevel.Info, "Deathlink/StatusComponent", $"Sprite: {kvp.Key}");
      // }
    }

    public void Push(Message message)
    {
      messages.Add(message);
    }

    public override void Update(GameTime gameTime)
    {
      base.Update(gameTime);

      List<int> removed = new();
      foreach (Message message in messages)
      {
        message.time += Engine.RawDeltaTime;
        if (message.time >= message.length - timeOut)
        {
          message.show = false;
        }
        if (message.time >= message.length)
        {
          removed.Add(messages.IndexOf(message));
          message.time = message.length;
        }
      }

      foreach (int i in removed)
      {
        try
        {
          messages.RemoveAt(i);
        }
        catch (Exception e)
        {
          Logger.Log(LogLevel.Debug, "Deathlink/StatusComponent", $"couldn't remove message {e}, idk why this error happens because it works fine anyway?");
        }
      }
    }

    protected virtual void RenderContentWrap(GameTime gameTime, bool toBuffer)
    {
      MDraw.SpriteBatch.Begin(
          SpriteSortMode.Deferred,
          BlendState.AlphaBlend,
          SamplerState.LinearClamp,
          DepthStencilState.None,
          RasterizerState.CullNone,
          null,
          // Matrix.Identity
          Engine.ScreenMatrix
      );

      Render(gameTime);

      MDraw.SpriteBatch.End();
    }

    public override void Draw(GameTime gameTime)
    {
      base.Draw(gameTime);

      if (messages.Count > 0)
      {
        RenderContentWrap(gameTime, true);
      }
    }

    public void Render(GameTime gameTime)
    {
      int i = messages.Count - 1;
      foreach (Message message in messages)
      {
        RenderMessage(message, i);
        i--;
      }
    }

    void RenderMessage(Message message, int index)
    {
      float a = Ease.SineInOut(message.show ? (Math.Min(message.time, timeIn) / timeIn) : Math.Min(message.length - message.time, timeOut) / timeOut);

      string text = message.text;
      if (!string.IsNullOrEmpty(text) && Dialog.Language != null && CelesteNetClientFont.Font != null)
      {
        Vector2 size = CelesteNetClientFont.Measure(text);
        float height = size.Y + 4.0f;

        float x_pos = 75f;
        float y_pos = Engine.Height - 75f - height * index;

        DeathlinkModuleSettings settings = DeathlinkModule.Settings;
        if (settings.Status.VerticalPosition == VerticalPosition.Middle)
        {
          y_pos = Engine.Height / 2 - height * messages.Count / 2 + height * index;
        }
        else if (settings.Status.VerticalPosition == VerticalPosition.Top)
        {
          y_pos = 75f + height * index;
        }

        if (settings.Status.HorizontalPosition == HorizontalPosition.Center)
        {
          x_pos = Engine.Width / 2 - size.X / 2;
        }
        else if (settings.Status.HorizontalPosition == HorizontalPosition.Right)
        {
          x_pos = Engine.Width - 75f - size.X;
        }

        Vector2 anchor = new(x_pos, y_pos);
        Vector2 pos = anchor;

        if (message.type != MessageType.None)
        {
          try
          {
            Icon icon = icons[message.type];
            MTexture texture = icon.texture;

            float iconScale = MathHelper.Lerp(0.2f, icon.scale, Ease.CubeOut(a));

            if (!(texture?.Texture?.Texture?.IsDisposed ?? true))
            {
              if (message.type == MessageType.Message)
              {
                for (int x = -2; x <= 2; x++)
                  for (int y = -2; y <= 2; y++)
                    if (x != 0 || y != 0)
                      texture.DrawCentered(pos + new Vector2(x, y), Color.Black * a * a * a * a, iconScale, 0.0f);
              }
              texture.DrawCentered(pos, Color.White * a, iconScale, 0.0f);
            }

            pos += new Vector2(48f, 0f);
          }
          catch (Exception e)
          {
            LoadContent();
            Logger.Log(LogLevel.Debug, "Deathlink/StatusComponent", $"Error rendering message icon: {e}");
          }
        }


        CelesteNetClientFont.DrawOutline(text, pos + new Vector2(size.X * 0.5f, 0.0f), new(0.5f, 0.5f), Vector2.One * MathHelper.Lerp(0.8f, 1f, Ease.CubeOut(a)), Color.White * a, 2f, Color.Black * a * a * a * a);
      }
    }
  }

  public class Message
  {
    public MessageType type;
    public string text;
    public float timeText;
    public float length;
    public float time;
    public bool show;

    public Message(MessageType type, string text, float time)
    {
      this.type = type;
      this.text = text;
      this.length = time;
      this.time = 0f;
      this.show = true;
    }
  }

  public class Icon
  {
    public MTexture texture;
    public float scale;

    public Icon(MTexture texture, float scale)
    {
      this.texture = texture;
      this.scale = scale;
    }
  }

  public enum MessageType
  {
    None,
    Message,
    Death,
    Error,
  }

  public enum VerticalPosition
  {
    Top,
    Middle,
    Bottom
  }

  public enum HorizontalPosition
  {
    Left,
    Center,
    Right
  }
}
