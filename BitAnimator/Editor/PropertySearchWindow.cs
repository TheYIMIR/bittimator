
using AudioVisualization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class PropertySearchWindow : EditorWindow
{
	static Action<BitAnimator.RecordSlot> onSelected;
	static string searchText = String.Empty;
	static List<string> currentPath = new List<string>(10);
	static Vector2 scroll;
	static Node selected;
	static Dictionary<string, List<Node>> tree;
	static List<Node> nodes;
	static Node[] found;
	static GUIStyle searchStyle;
	static GUIStyle searchCancelStyle;
	static GUIStyle left;
	static GUIStyle mid;
	static GUIStyle evenStyle;
	static GUIStyle oddStyle;
	static Regex r = new Regex("[A-Z]*[0-9a-z]*", RegexOptions.Compiled);
	bool firstRepaint = true;

	void OnEnable()
	{
	}

	void OnGUI()
	{
		if(searchStyle == null)
		{
			searchStyle = (GUIStyle)"ToolbarSeachTextField";
			searchCancelStyle = (GUIStyle)"ToolbarSeachCancelButton";
			left = (GUIStyle)"GUIEditor.BreadcrumbLeft";
			mid = (GUIStyle)"GUIEditor.BreadcrumbMid";
			evenStyle = (GUIStyle)"ObjectPickerResultsEven";
			oddStyle = (GUIStyle)"ObjectPickerResultsOdd";

			evenStyle.padding.top = 2;
			evenStyle.padding.bottom = 2;
			evenStyle.stretchWidth = true;

			oddStyle.padding.top = 2;
			oddStyle.padding.bottom = 2;
			oddStyle.stretchWidth = true;
		}

		GUILayout.BeginHorizontal();
		
		GUI.SetNextControlName("SeachField");
		string oldText = searchText;
		searchText = GUILayout.TextField(searchText, searchStyle);
		if(GUILayout.Button(String.Empty, searchCancelStyle))
			searchText = String.Empty;
		GUILayout.EndHorizontal();
		if(searchText.Length > 0 && searchText != oldText)
			found = Filter(searchText);

		IEnumerable<Node> list;
		if(searchText.Length == 0)
		{
			GUILayout.BeginHorizontal();
			for(int i = 0; i < currentPath.Count; i++)
				if(GUILayout.Button(currentPath[i], i == 0 ? left : mid) && i != (currentPath.Count - 1))
				{
					currentPath.RemoveRange(i + 1, currentPath.Count - i - 1);
					selected = null;
				}
			GUILayout.EndHorizontal();

			string path = String.Join("/", currentPath.ToArray());
			list = tree[path];
		}
		else
		{
			list = found;
		}
		
		scroll = GUILayout.BeginScrollView(scroll);
		bool odd = false;
		foreach(Node i in list)
		{
			if(GUILayout.Toggle(i == selected, i.name, odd ? oddStyle : evenStyle))
				selected = i;
			odd = !odd;
		}
		GUILayout.EndScrollView();
		if(selected != null)
		{
			if(selected.end)
			{
				bool doubleClick = false;
				if(Event.current.type == EventType.Used && Event.current.clickCount == 2 && Event.current.button == 0)
				{
					doubleClick = true;
				}
				if(GUILayout.Button("OK", GUILayout.Width(80)) || doubleClick)
				{
					onSelected(selected.property);
					selected = null;
					Close();
				}
			}
			else
			{
				currentPath.Add(selected.name);
				selected = null;
			}
		}
		if(firstRepaint && Event.current.type == EventType.Repaint)
		{
			firstRepaint = false;
			GUI.FocusControl("SeachField");
		}
	}
	Node[] Filter(string searchText)
	{
		int[] matches = new int[nodes.Count];
		foreach(string tag in r
			.Matches(searchText)
			.OfType<Match>()
			.Select(m => m.Groups[0].Value.ToLower())
			.Where(m => m.Length > 0)
			.Concat(new string[] { searchText.ToLower() }))
		{
			for(int i = 0; i < nodes.Count; i++)
			{
				foreach(string nodeTag in nodes[i].tags)
					if(nodeTag.Contains(tag))
						--matches[i];
			}
		}
		Node[] result = nodes.ToArray();
		Array.Sort(matches, result);
		Array.Resize(ref result, matches.Count(match => match < 0));
		return result;
	}

	public static void SearchProperty(Rect rect, List<BitAnimator.RecordSlot> properties, Action<BitAnimator.RecordSlot> _onSelected)
	{
		currentPath.Clear();
		currentPath.Add("Main");
		selected = null;
		onSelected = _onSelected;
		nodes = new List<Node>(properties.Count);
		foreach(var slot in properties)
		{
			Node node = new Node();
			node.name = slot.description;
			node.fullPath = "Main/" + slot.description;
			node.tags = r.Matches(slot.description).OfType<Match>().Select(m => m.Groups[0].Value.ToLower()).Where(m => m.Length > 0).Distinct().ToArray();
			node.end = true;
			node.property = slot;
			nodes.Add(node);
}
		tree = BuildPropertiesTree(nodes);
		Vector2 size = new Vector2(320, 480);
		var instance = EditorWindow.CreateInstance<PropertySearchWindow>();
		instance.ShowAsDropDown(rect, size);
	}

	static Dictionary<string, List<Node>> BuildPropertiesTree(List<Node> nodes)
	{
		Dictionary<string, List<Node>> tree = new Dictionary<string, List<Node>>();
		foreach(Node node in nodes)
		{
			string[] hierarchy = node.fullPath.Split('/');
			string path = hierarchy[0];
			for(int level = 1; level < hierarchy.Length; level++)
			{
				List<Node> list;
				tree.TryGetValue(path, out list);
				if(list == null)
				{
					list = new List<Node>();
					tree.Add(path, list);
				}
				string name = hierarchy[level];
				if(!list.Any(n => n.name == name))
				{
					Node e = new Node();
					e.name = hierarchy[level];
					e.fullPath = path;
					e.end = level == (hierarchy.Length - 1);
					e.property = node.property;
					list.Add(e);
				}
				path = String.Concat(path, "/", hierarchy[level]);
			}
		}
		return tree;
	}

	class Node
	{
		public string name;
		public string fullPath;
		public string[] tags;
		public bool end;
		public BitAnimator.RecordSlot property;

	}
}