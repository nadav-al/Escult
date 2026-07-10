<#
Reference solver for Escult Sketch Notation (ESN) levels.
Implements Docs/ProcGen/01_Puzzle_Ruleset.md R2 (actions) over R5.2 (state)
and Docs/ProcGen/02_Generation_Pipeline.md 1.3 (reference solver requirements).

Input:  an .esn.txt file — canvas block, ONE blank line, then legend block.
Output: a single JSON object on stdout (see bottom of this file for the schema).

Usage:
  powershell -NoProfile -ExecutionPolicy Bypass -File solve_esn.ps1 -EsnPath <file> [-MaxCost 30]
#>
param(
  [Parameter(Mandatory=$true)][string]$EsnPath,
  [int]$MaxCost = 30,
  [int]$MaxStates = 300000
)

function Fail($msg){
  [PSCustomObject]@{ ok=$false; error=$msg } | ConvertTo-Json -Depth 6
  exit 1
}

if(-not (Test-Path $EsnPath)){ Fail "file not found: $EsnPath" }
$raw = Get-Content $EsnPath
$blank = ($raw | Select-String -Pattern '^\s*$' | Select-Object -First 1).LineNumber
if(-not $blank){ Fail "esn file must have a blank line separating canvas from legend" }
$canvasLines = $raw[0..($blank-2)] | Where-Object { $_.Trim().Length -gt 0 }
$legendLines = $raw[$blank..($raw.Count-1)]

if($canvasLines.Count -eq 0){ Fail "empty canvas" }
$W = $canvasLines[0].Length
foreach($l in $canvasLines){ if($l.Length -ne $W){ Fail "canvas rows must be equal width (expected $W, got $($l.Length) on '$l')" } }

$lint = New-Object System.Collections.ArrayList
$RESERVED = @('C','O','X')

# ---- parse canvas ----
$terrain=@{}; $gates=@{}; $altars=@{}; $doors=@()  # doors: list of @{id;cell;openInitial}
$girl0=$null; $cat0=$null
for($y=0; $y -lt $canvasLines.Count; $y++){
  $row = $canvasLines[$y]
  for($x=0; $x -lt $row.Length; $x++){
    $ch=[string]$row[$x]; $c="$x,$y"
    switch -CaseSensitive ($ch){
      '#' { $terrain[$c]='W' }
      '~' { $terrain[$c]='P' }
      '.' { $terrain[$c]='G' }
      '@' { $terrain[$c]='G'; if($girl0){ [void]$lint.Add("multiple '@' girl spawns") }; $girl0=$c }
      'C' { $terrain[$c]='G'; if($cat0){ [void]$lint.Add("multiple 'C' cat spawns") }; $cat0=$c }
      'X' { $terrain[$c]='G'; $doors += [PSCustomObject]@{ id="d$($doors.Count+1)"; cell=$c; openInitial=$false } }
      'O' { $terrain[$c]='G'; $doors += [PSCustomObject]@{ id="d$($doors.Count+1)"; cell=$c; openInitial=$true } }
      default {
        if($ch -match '^\d$'){ $terrain[$c]='G'; if($altars.ContainsKey($ch)){ [void]$lint.Add("duplicate altar id '$ch'") }; $altars[$ch]=$c }
        elseif($ch -cmatch '^[A-Z]$' -and $RESERVED -notcontains $ch){
          $terrain[$c]='G'   # default; over=PIT legend line can flip this below
          if(-not $gates.ContainsKey($ch)){ $gates[$ch]=@() }
          $gates[$ch]+=$c
        } else { [void]$lint.Add("unrecognized glyph '$ch' at ($x,$y)") }
      }
    }
  }
}
if(-not $girl0){ Fail "no '@' girl spawn found" }
$GATE_IDS=@($gates.Keys | Sort-Object)
$DOOR_IDS=@($doors | ForEach-Object { $_.id })
$doorCellOf=@{}; $doorOpen0=@{}
foreach($d in $doors){ $doorCellOf[$d.id]=$d.cell; $doorOpen0[$d.id]=$d.openInitial }

# V1 lint: perimeter must be all '#'
for($x=0;$x -lt $W;$x++){
  if($terrain["$x,0"] -ne 'W'){ [void]$lint.Add("V1: top border not wall at col $x") }
  if($terrain["$x,$($canvasLines.Count-1)"] -ne 'W'){ [void]$lint.Add("V1: bottom border not wall at col $x") }
}
for($y=0;$y -lt $canvasLines.Count;$y++){
  if($terrain["0,$y"] -ne 'W'){ [void]$lint.Add("V1: left border not wall at row $y") }
  if($terrain["$($W-1),$y"] -ne 'W'){ [void]$lint.Add("V1: right border not wall at row $y") }
}

# ---- parse legend ----
# Single-door shorthand (R5.4): X/O may be used directly as a wiring target
# instead of its assigned id when there is exactly one door.
$doorAlias=@{}
if($DOOR_IDS.Count -eq 1){ $doorAlias['X']=$DOOR_IDS[0]; $doorAlias['O']=$DOOR_IDS[0] }

function ResolveTarget([string]$t){
  if($gates.ContainsKey($t)){ return $t }
  if($DOOR_IDS -contains $t){ return $t }
  if($doorAlias.ContainsKey($t)){ return $doorAlias[$t] }
  return $null
}

$SOULS0=9; $WIRING=@{}; $gateInitial=@{}; $gateOverPit=@{}; $decoyIds=@()
foreach($gid in $GATE_IDS){ $gateInitial[$gid]=$true; $gateOverPit[$gid]=$false }  # default CLOSED, over GROUND
foreach($line in $legendLines){
  $l = ($line -split '#',2)[0].Trim()
  if($l.Length -eq 0){ continue }
  if($l -match '^souls\s*:\s*(\d+)$'){ $SOULS0=[int]$Matches[1]; continue }
  if($l -match '^decoys\s*:\s*(.+)$'){ $decoyIds = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() }); continue }
  if($l -match '^(\d)\s*->\s*(.+)$'){
    $aid=$Matches[1]; $rawTargets=@($Matches[2] -split ',' | ForEach-Object { $_.Trim() })
    if(-not $altars.ContainsKey($aid)){ [void]$lint.Add("wiring references undefined altar '$aid'") }
    $resolved=@()
    foreach($t in $rawTargets){
      $r = ResolveTarget $t
      if($null -eq $r){ [void]$lint.Add("altar $aid targets undefined element '$t'") } else { $resolved+=$r }
    }
    if(-not $WIRING.ContainsKey($aid)){ $WIRING[$aid]=@() }
    $WIRING[$aid]+=$resolved
    continue
  }
  if($l -match '^([A-Za-z0-9]+)\s*:\s*(.+)$'){
    $gid=$Matches[1]; $rest=$Matches[2]
    if($gid -eq 'X' -or $gid -eq 'O' -or $DOOR_IDS -contains $gid){ continue }  # door state already encoded on canvas
    if(-not $gates.ContainsKey($gid)){ [void]$lint.Add("legend configures undefined gate '$gid'"); continue }
    if($rest -match 'initial\s*=\s*OPEN'){ $gateInitial[$gid]=$false }
    elseif($rest -match 'initial\s*=\s*CLOSED'){ $gateInitial[$gid]=$true }
    if($rest -match 'over\s*=\s*PIT'){ $gateOverPit[$gid]=$true; foreach($cell in $gates[$gid]){ $terrain[$cell]='P' } }
    continue
  }
  [void]$lint.Add("unparsed legend line: '$l'")
}
foreach($a in $altars.Keys){ if(-not $WIRING.ContainsKey($a)){ [void]$lint.Add("altar '$a' has no wiring (fires with no effect)") } }
foreach($id in $decoyIds){
  $known = $altars.ContainsKey($id) -or $gates.ContainsKey($id) -or ($DOOR_IDS -contains $id)
  if(-not $known){ [void]$lint.Add("decoy id '$id' does not match any altar/gate/door") }
}

$ALTAR_CELLS=@($altars.Values)
$hasCat = [bool]$cat0
$DIRVECS=@{ N=@(0,-1); S=@(0,1); E=@(1,0); W=@(-1,0) }

function Adj([string]$c){
  $p=$c -split ','; $x=[int]$p[0]; $y=[int]$p[1]
  @("$($x+1),$y","$($x-1),$y","$x,$($y+1)","$x,$($y-1)")
}
function ClosedGateCells([int]$gb){
  $s=@{}
  for($i=0;$i -lt $GATE_IDS.Count;$i++){ if((($gb -shr $i) -band 1) -eq 0){ foreach($c in $gates[$GATE_IDS[$i]]){ $s[$c]=1 } } }
  $s
}
function DoorCellSet(){ $s=@{}; foreach($d in $doors){ $s[$d.cell]=1 }; $s }
$DOOR_CELLS = DoorCellSet
function Walkable([string]$c,[hashtable]$cgc,[hashtable]$br){
  if(-not $terrain.ContainsKey($c)){ return $false }
  if($terrain[$c] -eq 'W'){ return $false }
  if($ALTAR_CELLS -contains $c -or $DOOR_CELLS.ContainsKey($c)){ return $false }
  if($cgc.ContainsKey($c)){ return $false }
  return ($terrain[$c] -eq 'G' -or $br.ContainsKey($c))
}
function ThrowLand([string]$src,[string]$dir,[hashtable]$cgc){
  $v=$DIRVECS[$dir]; $p=$src -split ','; $x=[int]$p[0]; $y=[int]$p[1]; $prev=$src
  while($true){
    $x+=$v[0]; $y+=$v[1]; $c="$x,$y"
    if(-not $terrain.ContainsKey($c)){ return $null }   # off-grid: never terminates (V1 violation)
    if($terrain[$c] -eq 'W' -or $cgc.ContainsKey($c) -or $DOOR_CELLS.ContainsKey($c)){
      if($ALTAR_CELLS -contains $prev){ return $null }  # illegal landing on an altar
      return $prev
    }
    $prev=$c
  }
}
function BrSet([string]$s){ $h=@{}; if($s){ foreach($b in $s -split '_'){ $h[$b]=1 } }; $h }
function BrAdd([string]$s,[string]$cell){ $l=@(); if($s){ $l=$s -split '_' }; ($l+$cell|Sort-Object) -join '_' }
function Won([string]$girl,[int]$db){
  for($i=0;$i -lt $DOOR_IDS.Count;$i++){ if((($db -shr $i) -band 1) -and ((Adj $girl) -contains $doorCellOf[$DOOR_IDS[$i]])){ return $true } }
  $false
}

function Successors([string]$st){
  $f=$st -split ';',6
  $girl=$f[0]; $cat=$f[1]; $souls=[int]$f[2]; $gb=[int]$f[3]; $db=[int]$f[4]; $brStr=$f[5]
  $cgc=ClosedGateCells $gb; $br=BrSet $brStr
  $out=New-Object System.Collections.ArrayList
  foreach($n in Adj $girl){ if(Walkable $n $cgc $br){ [void]$out.Add(@(0,"MOVE_GIRL:$n","$n;$cat;$souls;$gb;$db;$brStr")) } }
  if($hasCat -and $cat -ne 'HELD' -and $cat -ne 'DEAD'){
    foreach($n in Adj $cat){ if(Walkable $n $cgc $br){ [void]$out.Add(@(0,"MOVE_CAT:$n","$girl;$n;$souls;$gb;$db;$brStr")) } }
    if($cat -eq $girl -or (Adj $girl) -contains $cat){ [void]$out.Add(@(0,"PICKUP","$girl;HELD;$souls;$gb;$db;$brStr")) }
    if($souls -ge 1){
      foreach($a in $altars.Keys){
        if((Adj $altars[$a]) -contains $cat){
          $crush=$false
          foreach($t in $WIRING[$a]){ if($gates.ContainsKey($t) -and ($gates[$t] -contains $girl)){ $crush=$true } }
          if(-not $crush){
            $ngb=$gb; $ndb=$db
            foreach($t in $WIRING[$a]){
              if($gates.ContainsKey($t)){ $ngb=$ngb -bxor (1 -shl [array]::IndexOf($GATE_IDS,$t)) }
              else { $ndb=$ndb -bxor (1 -shl [array]::IndexOf($DOOR_IDS,$t)) }
            }
            $nc=$cat; if($souls -eq 1){ $nc='DEAD' }
            [void]$out.Add(@(1,"SACRIFICE_$a","$girl;$nc;$($souls-1);$ngb;$ndb;$brStr"))
          }
        }
      }
      foreach($p in Adj $cat){
        if($terrain.ContainsKey($p) -and $terrain[$p] -eq 'P' -and -not $br.ContainsKey($p) -and -not $cgc.ContainsKey($p)){
          $nb=BrAdd $brStr $p
          $nc=$cat; if($souls -eq 1){ $nc='DEAD' }
          [void]$out.Add(@(1,"BRIDGE_$p","$girl;$nc;$($souls-1);$gb;$db;$nb"))
        }
      }
    }
  }
  if($hasCat -and $cat -eq 'HELD'){
    [void]$out.Add(@(0,"DROP","$girl;$girl;$souls;$gb;$db;$brStr"))
    foreach($d in $DIRVECS.Keys){
      $land=ThrowLand $girl $d $cgc
      if($null -eq $land){ continue }
      if(Walkable $land $cgc $br){ [void]$out.Add(@(0,"THROW_$d","$girl;$land;$souls;$gb;$db;$brStr")) }
      elseif($terrain[$land] -eq 'P' -and $souls -ge 1){
        $nc=$girl; if($souls -eq 1){ $nc='DEAD' }
        [void]$out.Add(@(1,"THROW_$d(pit)","$girl;$nc;$($souls-1);$gb;$db;$brStr"))
      }
    }
  }
  ,$out
}

function Solve([string]$start,[int]$maxCost,[int]$maxStates){
  $buckets=@(); for($i=0;$i -le $maxCost;$i++){ $buckets+=,(New-Object System.Collections.Queue) }
  $paths=@{}; $best=@{}; $visited=0
  $buckets[0].Enqueue($start); $paths[$start]=@()
  for($cost=0;$cost -le $maxCost;$cost++){
    while($buckets[$cost].Count -gt 0){
      $st=$buckets[$cost].Dequeue()
      $visited++
      if($visited -gt $maxStates){ return @{ ok=$false; error="state space exceeded $maxStates - level likely malformed (e.g. unterminated throw) or too large for exhaustive search" } }
      $f=$st -split ';',6
      if(Won $f[0] ([int]$f[4])){ return @{ ok=$true; cost=$cost; path=$paths[$st]; finalState=$st; statesVisited=$visited } }
      $key="$($f[0]);$($f[1]);$($f[3]);$($f[4]);$($f[5])"
      if($best.ContainsKey($key) -and $best[$key] -le $cost){ continue }
      $best[$key]=$cost
      foreach($e in (Successors $st)){
        $c=$e[0]; $name=$e[1]; $ns=$e[2]
        $nf=$ns -split ';',6; $nkey="$($nf[0]);$($nf[1]);$($nf[3]);$($nf[4]);$($nf[5])"
        if($best.ContainsKey($nkey) -and $best[$nkey] -le ($cost+$c)){ continue }
        if($c -gt 0 -or -not $paths.ContainsKey($ns)){ $paths[$ns]=$paths[$st]+@($name) }
        $buckets[$cost+$c].Enqueue($ns)
      }
    }
  }
  @{ ok=$false; error="no solution found within soul/cost budget $maxCost" }
}

$db0=0
for($i=0;$i -lt $DOOR_IDS.Count;$i++){ if($doorOpen0[$DOOR_IDS[$i]]){ $db0=$db0 -bor (1 -shl $i) } }
$catStart = if($hasCat){ $cat0 } else { 'NONE' }
$start = "$girl0;$catStart;$SOULS0;0;$db0;"

# spawn sanity (V5): at least one legal move each from the initial state
$initCgc = ClosedGateCells 0; $initBr = BrSet ''
$girlMoves = @(Adj $girl0 | Where-Object { Walkable $_ $initCgc $initBr }).Count
if($girlMoves -eq 0){ [void]$lint.Add("V5: girl spawn has no legal initial move") }
if($hasCat){
  $catMoves = @(Adj $cat0 | Where-Object { Walkable $_ $initCgc $initBr }).Count
  if($catMoves -eq 0){ [void]$lint.Add("V5: cat spawn has no legal initial move") }
}

$result = Solve $start $MaxCost $MaxStates

$out = [ordered]@{
  ok = $true
  file = (Resolve-Path $EsnPath).Path
  bounds = @{ w = $W; h = $canvasLines.Count }
  soulBudget = $SOULS0
  gates = $GATE_IDS
  altars = @($altars.Keys | Sort-Object)
  doors = $DOOR_IDS
  decoys = $decoyIds
  lint = $lint
  solvable = $result.ok
}
if($result.ok){
  $out.minCost = $result.cost
  $out.slack = $SOULS0 - $result.cost
  $out.witness = $result.path
  $out.statesVisited = $result.statesVisited
  $usedIds = New-Object System.Collections.Generic.HashSet[string]
  foreach($step in $result.path){
    if($step -match '^SACRIFICE_(.+)$'){ [void]$usedIds.Add($Matches[1]) }
    foreach($gid in $GATE_IDS){ if($step -match "^BRIDGE_"){} }
  }
  $out.altarsUsedInWitness = @($usedIds)
  $out.unusedNonDecoyAltars = @($altars.Keys | Where-Object { $decoyIds -notcontains $_ -and -not $usedIds.Contains($_) })
} else {
  $out.error = $result.error
}
$out | ConvertTo-Json -Depth 8
