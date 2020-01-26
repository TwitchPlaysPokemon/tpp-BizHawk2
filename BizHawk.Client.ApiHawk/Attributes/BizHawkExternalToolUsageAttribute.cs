﻿using System;

using BizHawk.Client.Common;

namespace BizHawk.Client.ApiHawk
{
	/// <summary>
	/// This class holds logic interaction for the BizHawkExternalToolUsageAttribute
	/// This attribute helps ApiHawk to know how a tool can be enabled or not
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class BizHawkExternalToolUsageAttribute : Attribute
	{
		#region cTor(s)

		/// <exception cref="InvalidOperationException">
		/// <paramref name="usage"/> is <see cref="BizHawkExternalToolUsage.EmulatorSpecific"/> and <paramref name="system"/> is <see cref="CoreSystem.Null"/>, or
		/// usage is <see cref="BizHawkExternalToolUsage.GameSpecific"/> and <paramref name="gameHash"/> is blank
		/// </exception>
		public BizHawkExternalToolUsageAttribute(BizHawkExternalToolUsage usage, CoreSystem system, string gameHash)
		{
			if (usage == BizHawkExternalToolUsage.EmulatorSpecific && system == CoreSystem.Null)
			{
				throw new InvalidOperationException("A system must be set");
			}
			if (usage == BizHawkExternalToolUsage.GameSpecific && gameHash.Trim() == "")
			{
				throw new InvalidOperationException("A game hash must be set");
			}

			ToolUsage = usage;
			System = system;
			GameHash = gameHash;
		}

		/// <summary>
		/// Initialize a new instance of <see cref="BizHawkExternalToolUsageAttribute"/>
		/// </summary>
		/// <param name="usage"><see cref="BizHawkExternalToolUsage"/> i.e. what your external tool is for</param>
		/// <param name="system"><see cref="CoreSystem"/> that your external tool is used for</param>		
		public BizHawkExternalToolUsageAttribute(BizHawkExternalToolUsage usage, CoreSystem system)
			:this(usage, system, "")
		{}

		/// <summary>
		/// Initialize a new instance of <see cref="BizHawkExternalToolUsageAttribute"/>
		/// </summary>
		public BizHawkExternalToolUsageAttribute()
			:this(BizHawkExternalToolUsage.Global, CoreSystem.Null, "")
		{ }


		#endregion

		#region Properties

		/// <summary>
		/// Gets the specific system used by the external tool
		/// </summary>
		public CoreSystem System { get; }
		

		/// <summary>
		/// Gets the specific game (hash) used by the external tool
		/// </summary>
		public string GameHash { get; }
		
		/// <summary>
		/// Gets the tool usage
		/// </summary>
		public BizHawkExternalToolUsage ToolUsage { get; }

		#endregion
	}
}
