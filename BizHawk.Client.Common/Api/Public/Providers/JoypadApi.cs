using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Api.Public.Providers
{
	class JoypadApi : ApiProvider
	{
		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>() {
			new ApiCommand("GetButtons", args=>string.Join(" ", Buttons), new List<ApiParameter>(), "Gets the possible joypad buttons for the current core"),
			new ApiCommand("GetPressedButtons", args=>string.Join(" ", PressedButtons), new List<ApiParameter>(), "Gets the currently pressed joypad buttons"),
			new ApiCommand("SetButtons", args=>Buttons = ParseButtonString(args?.FirstOrDefault()), new List<ApiParameter>(){ Params.Buttons }, "Sets the pressed joypad buttons"),
			new ApiCommand("QueueInput",
				args=>InputQueue.Enqueue(new Input(ParseButtonString(args?.FirstOrDefault()), int.Parse(args?.ElementAtOrDefault(1) ?? "0"),int.Parse(args?.ElementAtOrDefault(2) ?? "0"), (args?.Count() ?? 0) > 3 ? string.Join("/", args?.Skip(3)) : null)),
				new List<ApiParameter>(){ Params.Buttons, Params.HeldFrames, Params.SleepFrames, Params.Callback},
				"Queues an input with given Buttons that is held for given HeldFrames and sleeps for given SleepFrames. When the input is finished, Callback will be called (if provided)."
			),
			new ApiCommand("HoldFrames", args=>(HoldFrames = int.Parse(args.FirstOrDefault() ?? HoldFrames.ToString())).ToString(), new List<ApiParameter>() {Params.Frames }, "Sets the number of Frames a held input (containing Hold) will last"),
		};

		private static int HoldFrames { get; set; } = 24;

		private static class Params
		{
			public static ApiParameter Buttons = new ApiParameter("Buttons", "string");
			public static ApiParameter Frames = new ApiParameter("Frames", "int(dec)");
			public static ApiParameter HeldFrames = new ApiParameter("HeldFrames", "int(dec)", optional: true);
			public static ApiParameter SleepFrames = new ApiParameter("SleepFrames", "int(dec)", optional: true);
			public static ApiParameter Callback = new ApiParameter("CallbackUrl", "url", true);
		}

		public class Input
		{
			public IEnumerable<string> Buttons { get; private set; }
			public int HeldFrames { get; private set; }
			public int SleepFrames { get; private set; }
			public string CallbackUrl { get; private set; }
			private int TotalFrames { get; set; }
			public bool IsHoldInput => Buttons.Contains("Hold");
			public bool ShouldHoldPastEnd => SleepFrames < 0;
			public bool IsSleeping => TotalFrames > HeldFrames && (!ShouldHoldPastEnd || TotalFrames > HeldFrames + SleepFrames + HoldFrames);
			public bool IsExpired => TotalFrames >= HeldFrames + SleepFrames;

			public Input(IEnumerable<string> buttons, int heldFrames = 0, int sleepFrames = 0, string callbackUrl = null)
			{
				Buttons = buttons;
				HeldFrames = heldFrames;
				SleepFrames = sleepFrames;
				CallbackUrl = callbackUrl;
				if (IsHoldInput)
				{
					var shiftFrames = HoldFrames - HeldFrames;
					HeldFrames += shiftFrames;
					SleepFrames -= shiftFrames;
					if (ShouldHoldPastEnd)
					{
						HeldFrames += SleepFrames;
					}
				}
			}

			public void Tick(int frames = 1)
			{
				TotalFrames += frames;
				OnExpired();
			}

			private void OnExpired()
			{
				if (IsExpired)
				{
					if (!string.IsNullOrWhiteSpace(CallbackUrl))
					{
						PingHttp(CallbackUrl);
					}
				}
			}
		}

		private static async void PingHttp(string url)
		{
			try
			{

				await WebRequest.Create(new Uri(url, UriKind.Absolute)).GetResponseAsync();
			}
			catch { }
		}

		private Queue<Input> InputQueue = new Queue<Input>();

		public override void OnFrame(int frameCount)
		{
			if (InputQueue.Any())
			{
				var currInput = InputQueue.Peek();
				if (!currInput.IsSleeping)
				{
					Buttons = currInput.Buttons;
				}
				currInput.Tick();
				if (currInput.IsExpired && InputQueue.Count > 1)
				{
					InputQueue.Dequeue();
				}
			}
		}

		public IEnumerable<string> Buttons
		{
			get => Global.AutofireStickyXORAdapter.Source.Definition.BoolButtons.Concat(new string[] { "Hold" });
			set
			{
				foreach (var button in value.Where(b => b != "Hold"))
				{
					Global.ButtonOverrideAdaptor.SetButton(button, true);
				}
				Global.ActiveController.Overrides(Global.ButtonOverrideAdaptor);
			}
		}

		public IEnumerable<string> PressedButtons => Buttons.Where(b => Global.AutofireStickyXORAdapter.IsPressed(b));

		public IEnumerable<string> ParseButtonString(string buttons) => buttons.Split('+').Select(b => Buttons.FirstOrDefault(kb => string.Equals(b, kb, StringComparison.OrdinalIgnoreCase))).Where(b => !string.IsNullOrWhiteSpace(b));
	}
}
