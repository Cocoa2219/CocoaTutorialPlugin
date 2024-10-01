using System;
using System.Diagnostics.CodeAnalysis;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace CocoaTutorialPlugin.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class Ping : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        if (arguments.Count < 1)
        {
            response = "사용법: ping <x> <y> <z> [type]";
            return false;
        }

        if (!float.TryParse(arguments.At(0), out var x) || !float.TryParse(arguments.At(1), out var y) || !float.TryParse(arguments.At(2), out var z))
        {
            response = "좌표는 숫자여야 합니다.";
            return false;
        }

        var position = new UnityEngine.Vector3(x, y, z);
        if (!Enum.TryParse(arguments.At(3), true, out PingType type))
            type = PingType.Default;

        Features.Utils.Ping(position, type);
        response = "성공적으로 핑을 생성했습니다.";
        return true;
    }

    public string Command { get; } = "ping";
    public string[] Aliases { get; } = { "p" };
    public string Description { get; } = "특정 위치에 SCP-079의 핑을 생성합니다.";
}

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class PingOnly : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        if (arguments.Count < 1)
        {
            response = "사용법: pingonly <target> <x> <y> <z> [type]";
            return false;
        }

        if (!float.TryParse(arguments.At(1), out var x) || !float.TryParse(arguments.At(2), out var y) || !float.TryParse(arguments.At(3), out var z))
        {
            response = "좌표는 숫자여야 합니다.";
            return false;
        }

        var target = Player.Get(arguments.At(0));

        if (target == null)
        {
            response = "플레이어를 찾을 수 없습니다.";
            return false;
        }

        var position = new UnityEngine.Vector3(x, y, z);

        if (!Enum.TryParse(arguments.At(4), true, out PingType type))
            type = PingType.Default;

        Features.Utils.Ping(position, type, target);
        response = "성공적으로 핑을 생성했습니다.";
        return true;
    }

    public string Command { get; } = "pingonly";
    public string[] Aliases { get; } = { "po" };
    public string Description { get; } = "특정 위치에 SCP-079의 핑을 생성합니다. (특정 플레이어에게만)";
}