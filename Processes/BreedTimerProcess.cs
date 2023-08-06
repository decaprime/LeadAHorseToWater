using System;
using System.Collections;
using BepInEx.Logging;
using Unity.DebugDisplay;
using UnityEngine;

namespace LeadAHorseToWater;

public class BreedTimerProcess
{
	private static ManualLogSource _log => Plugin.LogInstance;
	public static BreedTimerProcess Instance;
	private readonly float _breedCooldown = Settings.HORSE_BREED_COOLDOWN.Value;
	private float _timePassed = 0f;
	private float _remainingTime = Settings.HORSE_BREED_COOLDOWN.Value;

	public float RemainingTime => _remainingTime;

	public bool IsBreedCooldownActive { get; set; }

	public void Setup()
	{
		Instance = this;
	}

	public void StartCooldown()
	{
		IsBreedCooldownActive = true;
	}

	public void StopCooldown()
	{
		IsBreedCooldownActive = false;
		_remainingTime = _breedCooldown;
	}

	public void Update()
	{
		if (!Settings.ENABLE_HORSE_BREED_COOLDOWN.Value) return;
		if (IsBreedCooldownActive)
		{
			_timePassed += 1;
			_remainingTime = _breedCooldown - _timePassed;
		}

		if (!(_remainingTime <= 0f)) return;
		_log.LogDebug($"Breed cooldown has ended!");
		StopCooldown();
	}
}
