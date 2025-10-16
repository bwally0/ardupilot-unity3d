using namespace System.Collections.Generic

# !!!
# lib created by: codebykyle

# Encapsulate an arbitrary command
class PaneCommand {
    [string]$Command

    PaneCommand() {
        $this.Command = "";
    }

    PaneCommand([string]$command) {
        $this.Command = $command
    }

    [string]GetCommand() {
        return $this.Command
    }

    [string]ToString() {
        return $this.GetCommand();
    }
}

# A proxy for Split Pane which takes in a command to run inside the pane
class Pane : PaneCommand {
    [string]$ProfileName;
    [string]$Orientation
    [decimal]$Size;

    Pane([string]$command) : base($command) {
        $this.Orientation = '';
        $this.ProfileName = "Windows Powershell"
        $this.Size = 0.5;
    }

    Pane([string]$command, [string]$orientation) : base($command) {
        $this.Orientation = $orientation;
        $this.ProfileName = "Windows Powershell"
        $this.Size = 0.5;
    }

    Pane([string]$command, [string]$orientation, [decimal]$size) : base($command) {
        $this.Orientation = $orientation;
        $this.ProfileName = "Windows Powershell"
        $this.size = $size;
    }
    
    Pane([string]$ProfileName, [string]$command, [string]$orientation, [decimal]$size) : base($command) {
        $this.Orientation = $orientation;
        $this.ProfileName = $ProfileName;
        $this.size = $size;
    }

    [string]GetCommand() {
        return 'split-pane --size {0} {1} -p "{2}" -c {3}' -f $this.Size, $this.Orientation, $this.ProfileName, $this.Command
    }
}

class TargetPane : PaneCommand {
    [int]$SelectedIndex;

    TargetPane([int]$index) {
        $this.SelectedIndex = $index;
    }

    [string]GetCommand() {
        return "focus-pane --target={0}" -f $this.SelectedIndex;
    }
}

class MoveFocus : PaneCommand {
    [string]$direction;

    MoveFocus([string]$direction) {
        $this.direction = $direction;
    }

    [string]GetCommand() {
        return 'move-focus --direction {0}' -f $this.direction;
    }
}

class PaneManager : PaneCommand {
    [string]$InitialCommand;
    [List[PaneCommand]]$PaneCommands;
    [string]$ProfileName;
    [string]$DefaultOrientation;
    [double]$DefaultSize;

    PaneManager() {
        $this.PaneCommands = [List[PaneCommand]]::new();
        $this.ProfileName = "Microsoft Powershell";
        $this.DefaultOrientation = '-H';
        $this.DefaultSize = 0.5;
        $this.InitialCommand = "--maximized"
    }

    PaneManager([string]$ProfileName) {
        $this.ProfileName = $ProfileName;
        $this.DefaultOrientation = '-H';
        $this.DefaultSize = 0.5;
    }

    [PaneManager]SetInitialCommand([string]$command) {
        $this.InitialCommand = $command;
        return $this;
    }

    [PaneManager]SetProfileName([string]$name) {
        $this.ProfileName = $name;
        return $this;
    }

    [PaneManager]SetDefaultOrientation([string]$orientation) {
        $this.DefaultOrientation = $orientation;
        return $this;
    }

    [PaneManager]SetDefaultSize([double]$size) {
        $this.DefaultSize = $size;
        return $this;
    }

    [PaneManager]SetOptions([string]$name, [string]$orientation, [double]$size) {
        return $this.SetProfileName($name)
                .SetDefaultOrientation($orientation)
                .SetDefaultSize($size);

    }

    [PaneManager]AddPane([PaneManager]$manager) {
        $manager.SetInitialCommand('');
        $this.AddCommand($manager);
        return $this;
    }

    [PaneManager]AddCommand([PaneCommand]$command) {
        $this.PaneCommands.Add($command);
        return $this;
    }

    [PaneManager]AddPane([string]$command, [string]$orientation, [decimal]$size) {
        $newPane = $this.MakePane(
            $this.ProfileName, 
            $command, 
            $orientation,
            $size
        );

        $this.AddCommand($newPane);
        return $this;
    }

    [Pane]MakePane($ProfileName, $command, $orientation, $size) {
        $newPane = [Pane]::new($ProfileName, $command, $orientation, $size);
        return $newPane;
    }

    [PaneManager]TargetPane([int]$index) {
        $targetCommand = [TargetPane]::new($index)
        $this.AddCommand($targetCommand)
        return $this;
    }

    [PaneManager]MoveFocus([string]$direction) {
        $targetCommand = [MoveFocus]::new($direction)
        $this.AddCommand($targetCommand)
        return $this;
    }

    [int]GetPaneCount() {
        $count = 0;
        
        foreach ($command in $this.PaneCommands)
        {
            if ($command -is [PaneManager]) {
                $count += $command.GetPaneCount();
            } elseif ($command -is [PaneCommand]) {
                $count += 1;
            }
        }

        return $count;
    }

    [string]GetCommand() {
        
        $joinedCommands = $this.PaneCommands -join "; ";
        
        if ($joinedCommands -eq "") {
            return $this.InitialCommand;
        }

        $finalCommand =  if ($this.InitialCommand -ne "") { "{0}; {1}" -f $this.InitialCommand, $joinedCommands} else { $joinedCommands };
        return $finalCommand
    }
}


# script starts here
# clean up old logs
Remove-Item -Path (Join-Path $PSScriptRoot "*.log") -Force -ErrorAction SilentlyContinue

# create panes
$row1 = ([PaneManager]::new()).
                AddPane("uv run -m dss.manager 127.0.0.1 18300", '-H', 0.75).
                AddPane("uv run -m dss.user 127.0.0.1 18301 18302 --manager-ipv4 127.0.0.1 --manager-port 18300", '-V', 0.5);

$row2 = ([PaneManager]::new()).
                MoveFocus("left").
                AddPane("uv run -m dss.disk 127.0.0.1 18303 18304 --manager-ipv4 127.0.0.1 --manager-port 18300",  '-H', 0.66).
                MoveFocus("right").
                AddPane("uv run -m dss.disk 127.0.0.1 18305 18306 --manager-ipv4 127.0.0.1 --manager-port 18300", '-H', 0.66);

$row3 = ([PaneManager]::new()).
                MoveFocus("left").
                AddPane("uv run -m dss.disk 127.0.0.1 18307 18308 --manager-ipv4 127.0.0.1 --manager-port 18300", '-H', 0.5).
                MoveFocus("right").
                AddPane("uv run -m dss.disk 127.0.0.1 18309 18310 --manager-ipv4 127.0.0.1 --manager-port 18300", '-H', 0.5);

$paneManager = ([PaneManager])::new().
                AddPane($row1).
                AddPane($row2).
                AddPane($row3)

start wt $paneManager
