using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Class for an interior blocks of the level
/// </summary>
/// <remarks>For example, walls or floor tiles</remarks>
public class Interior : MonoBehaviour
{
	/// <summary>
	/// Position in the 
	/// </summary>
	private Vector2 _pos;
	/// <summary>
	/// Number of the tile
	/// </summary>
	/// <remarks>On map every tile is initialized by the number which it is given,
	/// it used to understand which sprite to use and if it is a wall or a floor</remarks>
	private int _interNum;
	/// <summary>
	/// Children objects
	/// </summary>
	/// <remarks>Only highest ones</remarks>
	private GameObject[] _children;

	public enum InteriorType
	{
		Floor,
		Wall
	}
	
	/// <summary>
	/// Initializes if the interior is is a part of a wall or a floor
	/// </summary>
	protected InteriorType PTileType;
	
	/// <summary>
	/// Property for an InteriorType
	/// </summary>
	public virtual InteriorType TileType
	{
		get => PTileType;
		set
		{
			PTileType = value;
			if (value != InteriorType.Wall) return;
			var l = 0;
			Interior tInterior;
			if (_children != null)
				l = _children.Length;
			for (var i = 0; i < l; i++)
				if (tInterior = _children[i].GetComponent<Interior>())
					tInterior.TileType = InteriorType.Wall;
		}
	}
	
	/// <summary>
	/// Interior initialization
	/// </summary>
	public void Awake()
	{
		_children = Array.Empty<GameObject>();
		var size = 0;
		foreach(Transform tTransform in transform)
		{
			Array.Resize(ref _children, ++size);
			_children[size - 1] = tTransform.gameObject;
		}
	}
	/// <summary>
	/// Sets sprites for all interior blocks which this interior consists from
	/// </summary>
	/// <param name="spriteNum">Number of the sprite to draw on the interior</param>
	public virtual void SetSprite(int spriteNum)
	{
		var l = 0;
		if (_children != null)
			l = _children.Length;
		for (var i = 0; i < l; i++)
		{
			if (spriteNum < 240 && _children[i].name.Contains("WallLight"))
			{
				var light2D = _children[i].GetComponent<UnityEngine.Rendering.Universal.Light2D>();
				light2D.color = Map.Colors[spriteNum % 224];
				light2D.enabled = true;
			}

			Interior tInterior;
			if (tInterior = _children[i].GetComponent<Interior>())
			{
				tInterior.SetSprite(spriteNum);
			}
		}
	}
	
	/// <summary>
	/// Sets interior by its coordinate at tilemap and number of the chosen interior
	/// </summary>
	/// <param name="eX">x coordinate on tilemap</param>
	/// <param name="eY">y coordinate on tilemap</param>
	/// <param name="interNum">Number of the interior</param>
	public void SetInterior(int eX, int eY, int interNum)
	{
		_interNum = interNum;
		_pos.x = eX + eY;
		_pos.y = (float)-0.5 * eX + (float)0.5 * eY;
		transform.localPosition = _pos;
		gameObject.name = eX.ToString("D3") + "x" + eY.ToString("D3");

	}
}
