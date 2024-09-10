namespace Jinroo;

public enum RoleType
{
  VILLAGER,
  WEREWOLF,
  NEUTRAL,
  NONE,
}

public enum RoleTeam
{
  NONE,
  VILLAGE,
  WEREWOLVES,
  NEUTRAL,
  LOVERS,
}


public enum RoleOrder
{
  CUPID,
  SEER,
  WEREWOLF,
  WITCH,
  HUNTER,
  VILLAGERS
}

public enum KillReason
{
  NONE,
  WEREWOLF,
  VILLAGE,
  WITCH,
  LOVER_IS_DEAD,
  HUNTER_LAST_STAND
}

public enum GamePhaseType
{
  UNKNOWN,
  STARTUP,
  DUSK,
  WEREWOLVES,
  VILLAGE_VOTE,
  WITCH,
  SEER,
  CUPID,
  HUNTER_LAST_STAND
}

public enum GameDayTime
{
  NIGHT,

  DAY,
}

public enum GameChannels
{
  GLOBAL,
  LOVERS,
  DEATH
}

public enum ServerMessageType
{
  INFO,
  VOTE,
  TURN,
  DEATH,
}