using Celeste.Mod.CelesteNet.DataTypes;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet;
using MDraw = Monocle.Draw;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System;

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
      //   Logger.Log(LogLevel.Info, "Deathlink", $"Sprite: {kvp.Key}");
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
          Logger.Log(LogLevel.Debug, "Deathlink", $"couldn't remove message {e}, idk why this error happens because it works fine anyway?");
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
          Matrix.Identity
      );

      Render(gameTime);

      MDraw.SpriteBatch.End();
    }

    public override void Draw(GameTime gameTime)
    {
      base.Draw(gameTime);

      RenderContentWrap(gameTime, true);
    }

    public void Render(GameTime gameTime)
    {
      if (messages.Count > 0)
      {
        int i = messages.Count - 1;
        foreach (Message message in messages)
        {
          RenderMessage(message, i);
          i--;
        }
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

        Vector2 anchor = new(96f, CelesteNetGameComponent.UI_HEIGHT - 96f - height * index);
        Vector2 pos = anchor;

        if (message.type != MessageType.None)
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
}
