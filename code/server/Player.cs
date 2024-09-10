using System;

namespace Jinroo;

public class Player
{
  public int index { get; protected set; }

  // ARole
  public ARole Role { get; protected set; }

  public PlayerController Controller { get; protected set; }

  public Connection Connection { get; set; }

  public PlayerState State { get; protected set; }

  public GameState GameState { get; protected set; }

  public GameMode GameMode { get; protected set; }

  public bool IsAlive { get; protected set; } = true;

  public List<KillReason> DeathReasons { get; protected set; } = new();

  public void AssignRole( ARole role )
  {
    if ( Role is not null )
    {
      // We readd its previous role in the role pool.
      GameMode.RemainingRoles.Add( Role );
      Role.Player = null;

      Role = null;
    }


    Role = role;
    Role.Player = this;

    // Send the assignment to concerned player
    Controller?.Client_AssignRole( role.GetKey() );
  }


  public Player( int playerIndex, GameMode gameMode, GameState gameState, PlayerState playerState, PlayerController playerController = null )
  {
    index = playerIndex;
    GameMode = gameMode;
    GameState = gameState;
    State = playerState;
    Controller = playerController;
  }

  public void TryKill( KillReason reason )
  {
    if ( !GameMode.PendingKills.Contains( this ) )
      GameMode.PendingKills.Add( this );

    if ( !DeathReasons.Contains( reason ) )
      DeathReasons.Add( reason );
  }

  public void Kill( bool announceDeath = true )
  {
    if ( !IsAlive )
      return;

    // GameState.Multicast_OnPlayerDead(playerIndex)
    // var pawn = GameplayStatics.GetPlayerData( index ).Pawn;
    // foreach ( var child in pawn.Children )
    // {
    //   if ( child.Name != "Camera" )
    //     child.Enabled = false;
    // }

    IsAlive = false;

    if ( announceDeath )
    {
      var announceDeathText = "";
      if ( DeathReasons.Contains( KillReason.LOVER_IS_DEAD ) )
        announceDeathText = $"As his soulmate has died... {State.Name} who was {Role.GetName().ToLower()} ended his life.";
      else if ( DeathReasons.Contains( KillReason.VILLAGE ) )
        announceDeathText = $"{State.Name} has been executed by the village ! He was {Role.GetName().ToLower()}.";
      else if ( DeathReasons.Contains( KillReason.HUNTER_LAST_STAND ) )
        announceDeathText = $"The hunter has decided to eliminate {State.Name} (${Role.GetName()}) before dying !";
      else
        announceDeathText = $"{State.Name} was {Role.GetName().ToLower()} has mysteriously died during the night !";

      GameState.Multicast_SendServerMessage( announceDeathText, ServerMessageType.DEATH );
    }


    LeaveChat( GameChannels.GLOBAL );
    JoinChat( GameChannels.DEATH );

    Controller?.Client_SendServerMessage( "You are dead. You can communicate freely here with players who have died.", ServerMessageType.INFO, GameChannels.DEATH );

    GameMode.GameState.Multicast_OnPlayerDead( index );
  }

  public void Save( KillReason killToSave )
  {
    if ( !GameMode.PendingKills.Contains( this ) )
      return;


    if ( DeathReasons.Contains( killToSave ) )
      DeathReasons.Remove( killToSave );

    if ( DeathReasons.Count == 0 )
      GameMode.PendingKills.Remove( this );
  }

  public Player VoteResult;

  public void VotePlayer( Player target )
  {
    if ( !GameMode.IsWaitingVote )
      return;

    if ( !GameMode.VoteParticipants.Contains( this ) || !GameMode.VoteChoices.Contains( target ) )
      return;

    var previousVote = VoteResult;

    // If the player has voted againt the same, we consider they want to remove its vote from this one.
    if ( VoteResult == target )
      target = null;

    VoteResult = target;


    GameMode.OnPlayerVote( this, target, previousVote );
  }

  public void RestrictVision() => Controller?.Client_RestrictVision();
  public void GiveVision() => Controller?.Client_GiveVision();

  public void JoinChat( GameChannels channel = GameChannels.GLOBAL )
  {
    GameMode.JoinChat( this, channel );
  }

  public void LeaveChat( GameChannels channel = GameChannels.GLOBAL )
  {
    GameMode.LeaveChat( this, channel );
  }

  public bool IsInChatChannel( GameChannels channel = GameChannels.GLOBAL )
  {
    var playersInChannel = GameMode.GetPlayersInChatChannel( channel );
    if ( playersInChannel is null )
      return false;

    return playersInChannel.Contains( this );
  }

  public void SendChatMessage( string message, GameChannels channel = GameChannels.GLOBAL )
  {
    if ( !IsInChatChannel( channel ) )
      return;

    var playersInChannel = GameMode.GetPlayersInChatChannel( channel );

    if ( playersInChannel is null )
      return;

    foreach ( var player in playersInChannel )
    {
      player.Controller?.Client_SendChatMessage( message, State.Name, channel );
    }
  }

  public void SendServerMessage( string message, ServerMessageType type = ServerMessageType.INFO, GameChannels channel = GameChannels.GLOBAL )
  {
    if ( !IsInChatChannel( channel ) )
      return;

    var playersInChannel = GameMode.GetPlayersInChatChannel( channel );

    if ( playersInChannel is null )
      return;

    foreach ( var player in playersInChannel )
    {
      player.Controller?.Client_SendServerMessage( message, type, channel );
    }
  }




  public void StartChoosingPlayer( List<Player> choices )
  {
    if ( Controller is null )
      return;


    // Controller.Client_StartChoosingPlayer
  }
}