using System;
using Sandbox.Network;

namespace Jinroo;

public sealed class GameLobby : Component, Component.INetworkListener
{
	[Property] public GameObject Spawn { get; set; }

	[Property] public GameObject PlayerPawnPrefab { get; set; }

	[Property] public GameObject UserInterface { get; set; }

	[Property] public int MaxBots { get; set; } = 0;

	private LobbyPanel LobbyHUD;

	private GameHUD InGameHUD;

	public GameMode GameMode;

	public GameChat Chat;

	public bool IsReadyToStart = false;

	protected override void OnUpdate()
	{
		// Log.Info( "Update" );
	}

	protected override void OnAwake()
	{
		base.OnAwake();

		LobbyHUD = UserInterface.Components.Get<LobbyPanel>( true );
		InGameHUD = UserInterface.Components.Get<GameHUD>( true );
		Chat = UserInterface.Components.Get<GameChat>( true );
	}


	protected override void OnStart()
	{
		if ( !GameNetworkSystem.IsActive )
		{
			GameNetworkSystem.CreateLobby();
		}

		base.OnStart();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( GameMode is not null )
		{
			GameMode.IsPlaying = false;
			GameMode.Destroy();
		}
	}


	public void OnConnected( Connection connection )
	{
	}

	public void OnActive( Connection connection )
	{
		Log.Info( $"PLayer '{connection.DisplayName}' ({connection.Id}) has joined the lobby" );
	}

	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"PLayer '{connection.DisplayName}' ({connection.Id}) has left the lobby" );
	}

	public void StartGame()
	{
		if ( !Networking.IsHost )
			return;

		CleanUpPreviousGame();

		Multicast_StartGame();
	}

	public void CleanUpPreviousGame()
	{
		if ( GameMode is null )
			return;

		var previousPlayerCount = GameplayStatics.GetPlayersCount();
		for ( var i = 0; i < previousPlayerCount; i++ )
		{
			var playerData = GameplayStatics.GetPlayerData( i );
			playerData.Controller?.GameObject.DestroyImmediate();
			playerData.State?.GameObject.DestroyImmediate();
			playerData.Pawn?.DestroyImmediate();
		}

		GameMode.GameState.GameObject?.DestroyImmediate();
		GameMode.GameObject.DestroyImmediate();

		GameMode = null;
	}

	public void ReturnFromGame()
	{
		SetUIPanel( false );
	}

	[Broadcast( NetPermission.HostOnly )]
	public void SetPawnOwner( Guid pawnId, Guid ownerId, Color tint )
	{

		var pawn = Scene.Directory.FindByGuid( pawnId );

		var model = pawn.Components.Get<SkinnedModelRenderer>( FindMode.InDescendants );
		model.Tint = tint;
	}


	public void SetUIPanel( bool isGameHUD )
	{

		var lobbyPanel = UserInterface.Components.Get<LobbyPanel>( true );
		var gameHUD = UserInterface.Components.Get<GameHUD>( true );
		// var chat = UserInterface.Components.Get<GameChat>( true );

		if ( isGameHUD )
		{
			if ( lobbyPanel is not null )
				lobbyPanel.Enabled = false;

			if ( gameHUD is not null )
				gameHUD.Enabled = true;

			// if ( chat is not null )
			// 	chat.Enabled = true;
		}
		else
		{
			if ( lobbyPanel is not null )
				lobbyPanel.Enabled = true;

			if ( gameHUD is not null )
				gameHUD.Enabled = false;

			// if ( chat is not null )
			// 	chat.Enabled = false;
		}
	}

	[Broadcast( NetPermission.HostOnly )]
	public void Client_SetPlayerIndex( Guid connectionGuid, int playerIndex )
	{
		var connection = Networking.Connections.Where( c => c.Id == connectionGuid ).First();

		if ( connection is not null )
		{
			// GameplayStatics.__SetPlayerIndex( connection, playerIndex );
			GameplayStatics.__SetPlayerConnection( playerIndex, connection );
		}
	}

	// [Broadcast( NetPermission.HostOnly )]
	// public void Multicast_StartGame()
	// {
	// 	GameplayStatics.__Reset();

	// 	if ( Networking.IsHost )
	// 	{
	// 		// Create the GameMode 
	// 		GameMode = new GameMode( this );

	// 		GameMode.SpawnOriginPosition = Spawn.Transform.Position;

	// 		List<Connection> participants = new();
	// 		foreach ( var player in Networking.Connections )
	// 		{
	// 			participants.Add( player );
	// 		}

	// 		Dictionary<string, int> roles = new();

	// 		roles["werewolf"] = 4;
	// 		// roles["witch"] = 1;
	// 		// roles["seer"] = 1;
	// 		// roles["hunter"] = 1;
	// 		// roles["cupid"] = 1;


	// 		GameMode.Start( participants, roles );

	// 	}

	// 	SetUIPanel( true );

	// 	// Waiting, InProgress
	// }

	[Broadcast( NetPermission.HostOnly )]
	public void Multicast_StartGame()
	{
		GameplayStatics.__Reset();

		SetUIPanel( true );

		if ( !GameplayStatics.IsHost() )
			return;

		// Create the new GameMode (only exists on the host)
		GameMode = GameMode.Create( this );
		GameplayStatics.__SetGameMode( GameMode );
		GameMode.SpawnOriginPosition = Spawn.Transform.Position;

		// Create the participants list 
		List<Connection> participants = new();
		foreach ( var player in Networking.Connections )
		{
			participants.Add( player );
		}

		// Create the roles
		Dictionary<string, int> roles = new();
		foreach ( KeyValuePair<string, int> pair in LobbyHUD.RolesAmount )
		{
			roles.Add( pair.Key, pair.Value );
		}

		int maxBot = LobbyHUD.MaxBot;
		int botsAmountToAdd = Math.Clamp( maxBot, 0, 16 - participants.Count );

		// We fix the roles setup if needed for avoiding issues. After that 
		// we are certain we have a valid setup and all players will have a role.
		GameplayStatics.FixRolesSetup( ref roles, participants.Count + botsAmountToAdd );

		// Start the game 
		GameMode.Start( participants, roles, maxBot );

		LobbyHUD.RolesAmount.Clear();
		LobbyHUD.PlayersReady.Clear();
	}

	[Broadcast( NetPermission.HostOnly )]
	public void Multicast_SetGameNetworkProps( List<GameNetworkProps> networkPropsList )
	{
		var gameState = Scene.GetAllComponents<GameState>().First();

		foreach ( var networkProps in networkPropsList )
		{
			var playerState = Scene.Directory.FindByGuid( networkProps.stateId )?.Components.Get<PlayerState>();
			if ( playerState is not null )
			{

				gameState?.AddPlayerState( playerState );
				GameplayStatics.__SetPlayerState( networkProps.index, playerState );
			}

			if ( networkProps.connectionId != Guid.Empty )
			{
				var connection = Networking.Connections.Where( c => c.Id == networkProps.connectionId ).FirstOrDefault();

				if ( connection is not null )
				{
					// GameplayStatics.__SetPlayerIndex( connection, networkProps.index );
					GameplayStatics.__SetPlayerConnection( networkProps.index, connection );

					if ( networkProps.controllerId != Guid.Empty )
					{
						if ( Connection.Local.Id == networkProps.connectionId )
						{
							var controller = Scene.Directory.FindByGuid( networkProps.controllerId )?.Components.Get<PlayerController>();
							GameplayStatics.__SetPlayerController( networkProps.index, controller );
							GameplayStatics.__SetLocalPlayerController( controller );
						}
					}
				}
			}
		}
	}
}
