﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using DPoint = System.Drawing.Point;

using TShockAPI;

namespace Terraria.Plugins.Common {
  /// <summary>
  ///   A user interaction class is meant to implement all logic required by a plugin to communicate with- and handle actions of players.
  ///   This includes command handling aswell as reacting to several events, such as player deaths etc.
  /// 
  ///   It supports asynchronous user interactions, so that "put a wire somewhere to display the name of this region" can be implemented
  ///   very easily.
  /// </summary>
  public abstract class UserInteractionHandlerBase: IDisposable {
    public const int CommandInteractionDefaultTimeoutMs = 20000;

    private readonly Dictionary<TSPlayer,CommandInteraction> activeCommandInteractions = 
      new Dictionary<TSPlayer,CommandInteraction>();
    private readonly object activeCommandInteractionsLock = new object();
    protected PluginTrace PluginTrace { get; private set; }
    protected Collection<Command> RegisteredCommands { get; private set; }


    protected UserInteractionHandlerBase(PluginTrace pluginTrace) {
      this.PluginTrace = pluginTrace;
      this.RegisteredCommands = new Collection<Command>();
      this.activeCommandInteractions = new Dictionary<TSPlayer,CommandInteraction>();
    }

    protected Command RegisterCommand(
      string[] names, CommandDelegate commandExec, Func<CommandArgs,bool> commandHelpExec = null, 
      string requiredPermission = null, bool allowServer = true, bool doLog = true
    ) {
      if (this.isDisposed) throw new ObjectDisposedException(this.ToString());
      if (names == null) throw new ArgumentNullException();
      if (commandExec == null) throw new ArgumentNullException();
      
      CommandDelegate actualCommandExec;
      if (commandHelpExec != null) {
        actualCommandExec = (args) => {
          if (args.ContainsParameter("help", StringComparison.InvariantCultureIgnoreCase))
            if (commandHelpExec(args))
              return;

          commandExec(args);
        };
      } else {
        actualCommandExec = commandExec;
      }

      Command command;
      if (requiredPermission != null)
        command = new Command(requiredPermission, actualCommandExec, names);
      else
        command = new Command(actualCommandExec, names);

      TShockAPI.Commands.ChatCommands.Add(command);
      command.AllowServer = allowServer;
      command.DoLog = doLog;

      return command;
    }

    protected void DeregisterCommand(Command tshockCommand) {
      if (tshockCommand == null) throw new ArgumentNullException();

      if (!TShockAPI.Commands.ChatCommands.Contains(tshockCommand))
        throw new InvalidOperationException("Command is not registered.");
    }

    protected CommandInteraction StartOrResetCommandInteraction(TSPlayer forPlayer, int timeoutMs = 0) {
      if (this.isDisposed) throw new ObjectDisposedException(this.ToString());
      if (forPlayer == null) throw new ArgumentNullException();

      CommandInteraction newInteraction = new CommandInteraction(forPlayer);

      lock (this.activeCommandInteractionsLock) {
        this.StopInteraction(forPlayer);
        this.activeCommandInteractions.Add(forPlayer, newInteraction);

        newInteraction.IsActive = true;
      }

      if (timeoutMs > -1) {
        if (timeoutMs == 0)
          timeoutMs = UserInteractionHandlerBase.CommandInteractionDefaultTimeoutMs;

        newInteraction.TimeoutMs = timeoutMs;
        newInteraction.TimeoutTimer = new System.Threading.Timer(
          this.InteractionTimeOutTimer_Callback, newInteraction, timeoutMs, Timeout.Infinite
        );
      }

      return newInteraction;
    }

    private void InteractionTimeOutTimer_Callback(object state) {
      CommandInteraction interaction = (state as CommandInteraction);
      if (interaction == null)
        return;

      lock (this.activeCommandInteractionsLock) {
        if (this.IsDisposed)
          return;

        if (!this.activeCommandInteractions.ContainsValue(interaction))
          return;

        if (interaction.ForPlayer.ConnectionAlive && interaction.IsActive) {
          if (interaction.TimeExpiredCallback != null) {
            try {
              interaction.TimeExpiredCallback(interaction.ForPlayer);
            } catch (Exception ex) {
              this.PluginTrace.WriteLineError("A command interaction's TimeExpiredCallback has thrown an exception:\n" + ex);
            }
          }
          if (interaction.AbortedCallback != null) {
            try {
              interaction.AbortedCallback(interaction.ForPlayer);
            } catch (Exception ex) {
              this.PluginTrace.WriteLineError("A command interaction's AbortedCallback has thrown an exception:\n" + ex);
            }
          }
          interaction.IsActive = false;
        }

        this.activeCommandInteractions.Remove(interaction.ForPlayer);
      }
    }

    protected void StopInteraction(TSPlayer forPlayer) {
      if (this.isDisposed) throw new ObjectDisposedException(this.ToString());
      if (forPlayer == null) throw new ArgumentNullException();

      lock (this.activeCommandInteractionsLock) {
        CommandInteraction interaction;
        if (this.activeCommandInteractions.TryGetValue(forPlayer, out interaction)) {
          this.activeCommandInteractions.Remove(forPlayer);

          if (interaction.AbortedCallback != null) {
            try {
              interaction.AbortedCallback(interaction.ForPlayer);
            } catch (Exception ex) {
              this.PluginTrace.WriteLineError("A command interaction's AbortedCallback has thrown an exception:\n" + ex);
            }
          }
          interaction.IsActive = false;
        }
      }
    }

    #region [Hook Handlers]
    public virtual bool HandleTileEdit(TSPlayer player, TileEditType editType, int blockType, DPoint location, int objectStyle) {
      if (this.IsDisposed || this.activeCommandInteractions.Count == 0)
        return false;

      lock (this.activeCommandInteractionsLock) {
        CommandInteraction interaction;
        // Is the player currently interacting with a command?
        if (!this.activeCommandInteractions.TryGetValue(player, out interaction))
          return false;

        if (interaction.TileEditCallback == null)
          return false;

        CommandInteractionResult result = interaction.TileEditCallback(player, editType, blockType, location, objectStyle);
        if (interaction.DoesNeverComplete)
          interaction.ResetTimer();
        else if (result.IsInteractionCompleted)
          this.activeCommandInteractions.Remove(player);

        return result.IsHandled;
      }
    }

    public virtual bool HandleChestGetContents(TSPlayer player, DPoint location) {
      if (this.IsDisposed || this.activeCommandInteractions.Count == 0)
        return false;

      lock (this.activeCommandInteractionsLock) {
        CommandInteraction interaction;
        // Is the player currently interacting with a command?
        if (!this.activeCommandInteractions.TryGetValue(player, out interaction))
          return false;

        if (interaction.ChestOpenCallback == null)
          return false;

        CommandInteractionResult result = interaction.ChestOpenCallback(player, location);
        if (interaction.DoesNeverComplete)
          interaction.ResetTimer();
        else if (result.IsInteractionCompleted)
          this.activeCommandInteractions.Remove(player);

        return result.IsHandled;
      }
    }

    public virtual bool HandleSignEdit(TSPlayer player, int signIndex, DPoint location, string newText) {
      if (this.IsDisposed || this.activeCommandInteractions.Count == 0)
        return false;

      lock (this.activeCommandInteractionsLock) {
        CommandInteraction interaction;
        // Is the player currently interacting with a command?
        if (!this.activeCommandInteractions.TryGetValue(player, out interaction))
          return false;

        if (interaction.SignEditCallback == null)
          return false;

        CommandInteractionResult result = interaction.SignEditCallback(player, signIndex, location, newText);
        if (interaction.DoesNeverComplete)
          interaction.ResetTimer();
        else if (result.IsInteractionCompleted)
          this.activeCommandInteractions.Remove(player);

        return result.IsHandled;
      }
    }

    public virtual bool HandleSignRead(TSPlayer player, DPoint location) {
      if (this.IsDisposed || this.activeCommandInteractions.Count == 0)
        return false;

      lock (this.activeCommandInteractionsLock) {
        CommandInteraction interaction;
        // Is the player currently interacting with a command?
        if (!this.activeCommandInteractions.TryGetValue(player, out interaction))
          return false;

        if (interaction.SignReadCallback == null)
          return false;
      
        CommandInteractionResult result = interaction.SignReadCallback(player, location);
        if (interaction.DoesNeverComplete)
          interaction.ResetTimer();
        else if (result.IsInteractionCompleted)
          this.activeCommandInteractions.Remove(player);

        return result.IsHandled;
      }
    }

    public virtual bool HandleHitSwitch(TSPlayer player, DPoint location) {
      if (this.IsDisposed || this.activeCommandInteractions.Count == 0)
        return false;

      CommandInteraction interaction;
      lock (this.activeCommandInteractionsLock) {
        // Is the player currently interacting with a command?
        if (!this.activeCommandInteractions.TryGetValue(player, out interaction))
          return false;

        if (interaction.HitSwitchCallback == null)
          return false;

        CommandInteractionResult result = interaction.HitSwitchCallback(player, location);
        if (interaction.DoesNeverComplete)
          interaction.ResetTimer();
        else if (result.IsInteractionCompleted)
          this.activeCommandInteractions.Remove(player);

        return result.IsHandled;
      }
    }
    #endregion

    #region [IDisposable Implementation]
    private bool isDisposed;

    public bool IsDisposed {
      get { return this.isDisposed; } 
    }

    protected virtual void Dispose(bool isDisposing) {
      if (this.isDisposed)
        return;

      if (isDisposing) {
        lock (this.activeCommandInteractionsLock) {
          this.activeCommandInteractions.Clear();
        }

        try {
          foreach (Command command in this.RegisteredCommands)
            if (TShockAPI.Commands.ChatCommands.Contains(command))
              TShockAPI.Commands.ChatCommands.Remove(command);
        // May occur due to unsynchronous thread operations.
        } catch (InvalidOperationException) {}
      }

      this.isDisposed = true;
    }

    public void Dispose() {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    ~UserInteractionHandlerBase() {
      this.Dispose(false);
    }
    #endregion
  }
}
