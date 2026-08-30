# -----------------------------------------------------------------------------
# map_grabber.tcl - capture a game's screens from openMSX to stitch its map
# -----------------------------------------------------------------------------
# Load it from the openMSX console:
#
#     source map_grabber.tcl
#     mapgrab::start capture.txt
#     ... play through the part of the map you want ...
#     mapgrab::stop
#
# Then open the file with MSX Game Tools, which does the stitching.
#
# --- When the name table has to be given by hand -----------------------------
# By default the name table is the one R#2 points at. Some games move it during
# the frame with the line interrupt -one table for the scoreboard at the top,
# another for the play area- and then reading it once a frame gets whichever was
# left set, which may well be the scoreboard's.
#
# Space Manbow does this: R#2 reads 0x1F at the end of the frame, and the
# terrain is in the table at 0x3F * 0x400 = 0xFC00. Give the address and it
# reads that one and no other:
#
#     mapgrab::start capture.txt 0xFC00
#
# To find it, dump the VRAM and the VDP registers from openMSX and open the dump
# with the VRAM import: it draws what each table holds, so it can be seen which
# one is the map.
#
# --- Why this script does not stitch anything --------------------------------
# It only writes down what the screen showed. Putting those screens together is
# the part with the interesting decisions -how much did the camera move, is this
# still the same room- and that lives in the editor, where it has tests and can
# be run again with other settings without replaying the game.
#
# So this file has one job and no cleverness: read the name table, and write it
# down when it changes.
#
# --- What gets written -------------------------------------------------------
# A text file: a small header, then one line per captured screen with the tile
# numbers of the name table, left to right and top to bottom.
#
# Only when it changes. A game that scrolls one cell every eight frames would
# otherwise write the same screen eight times, and a minute of play is already
# a few thousand frames.
#
# Each line also carries a checksum of the pattern table. When a game moves to
# another level it usually loads another tile set, and then the same tile
# numbers mean something else: whoever stitches this needs to know where to cut.
# It is cheap to write now and impossible to recover later without replaying.
# -----------------------------------------------------------------------------

namespace eval mapgrab {
    variable file ""
    variable previous ""
    variable screens 0

    # SCREEN 1 and 2 are 32x24 cells. Other modes are not what this captures.
    variable columns 32
    variable rows 24

    # The pattern table of one third, which is what the checksum covers.
    variable pattern_bytes 2048

    # The name table to read, or -1 to follow R#2. See the note at the top.
    variable names_at -1
}

# Reads a block of VRAM as a list of numbers.
#
# read_block is the fast way and openMSX has had it for a long time. If your
# build does not, the loop underneath does the same one byte at a time: it is
# slower but 768 bytes a frame is nothing.
proc mapgrab::read_vram {address size} {
    if {[catch {set data [debug read_block VRAM $address $size]} why]} {
        set bytes {}

        for {set at 0} {$at < $size} {incr at} {
            lappend bytes [debug read VRAM [expr {$address + $at}]]
        }

        return $bytes
    }

    binary scan $data cu* bytes

    return $bytes
}

# Where the VDP has each table right now.
#
# From the registers and not from a list of usual addresses: plenty of games put
# them somewhere else. In GRAPHIC 2 and 3 the low bits of R#3 and R#4 are a mask
# over the thirds and not part of the address, which is why only one bit of each
# counts here.
proc mapgrab::tables {} {
    set r0 [debug read "VDP regs" 0]
    set r1 [debug read "VDP regs" 1]
    set r2 [debug read "VDP regs" 2]
    set r3 [debug read "VDP regs" 3]
    set r4 [debug read "VDP regs" 4]

    set m3 [expr {($r0 & 0x02) != 0}]
    set m4 [expr {($r0 & 0x04) != 0}]

    # GRAPHIC 3 is M3 and M4 together, which is the R#0 = 0x06 row of the
    # manual. M4 on its own is TEXT 2, which this does not capture.
    if {$m3 && $m4} {
        set mode 3
    } elseif {$m4} {
        set mode 0
    } elseif {$m3} {
        set mode 2
    } else {
        set mode 1
    }

    # In GRAPHIC 2 and 3 the low bits of R#4 and R#3 are a mask over the thirds
    # and not part of the address, but the high ones are address: R#4 is six
    # bits, A16 to A11. On a V9938 with 128 KB the table goes well past 16 KB,
    # and looking at bit 2 alone -which is all an MSX1 needs- read the wrong
    # one. R#10 carries the top three bits of the colour table.
    set r10 [debug read "VDP regs" 10]

    if {$mode == 1} {
        set patterns [expr {($r4 & 0x3F) * 0x800}]
        set colors [expr {(($r10 & 0x07) << 14) | ($r3 * 0x40)}]
    } else {
        set patterns [expr {($r4 & 0x3C) * 0x800}]
        set colors [expr {(($r10 & 0x07) << 14) | (($r3 & 0x80) * 0x40)}]
    }

    return [list \
        mode $mode \
        names [expr {($r2 & 0x7F) * 0x400}] \
        patterns $patterns \
        colors $colors]
}

proc mapgrab::checksum {bytes} {
    set sum 0

    foreach byte $bytes {
        # Rotating rather than adding: with a plain sum, two tiles swapping
        # places would look like no change at all.
        set sum [expr {(($sum << 1) | (($sum >> 30) & 1)) ^ $byte}]
        set sum [expr {$sum & 0x7FFFFFFF}]
    }

    return $sum
}

# One capture. Re-arms itself, so stopping is what breaks the loop.
proc mapgrab::tick {} {
    variable file
    variable previous
    variable screens
    variable columns
    variable rows
    variable pattern_bytes

    if {$file eq ""} {
        return
    }

    variable names_at

    array set at [tables]

    set from [expr {$names_at >= 0 ? $names_at : $at(names)}]
    set names [read_vram $from [expr {$columns * $rows}]]

    if {$names ne $previous} {
        set stamp [checksum [read_vram $at(patterns) $pattern_bytes]]

        puts $file "$stamp $names"

        set previous $names
        incr screens
    }

    after frame [namespace code tick]
}

# Starts capturing into a file.
# Starts capturing. Give an address to read the name table from there instead of
# from wherever R#2 points; see the note at the top about games that move it.
proc mapgrab::start {path {names -1}} {
    variable file
    variable previous
    variable screens
    variable columns
    variable rows
    variable names_at

    if {$file ne ""} {
        error "already capturing; call mapgrab::stop first"
    }

    array set at [tables]

    if {$at(mode) != 1 && $at(mode) != 2 && $at(mode) != 3} {
        error "this only captures SCREEN 1, 2 and 4"
    }

    set names_at $names

    if {$names >= 0} {
        set at(names) $names
    }

    set file [open $path w]
    set previous ""
    set screens 0

    puts $file "msxmap 1"
    puts $file "columns $columns"
    puts $file "rows $rows"
    puts $file "mode $at(mode)"
    puts $file "names $at(names)"
    puts $file "patterns $at(patterns)"
    puts $file "colors $at(colors)"
    puts $file "screens"

    after frame [namespace code tick]

    return "capturing into $path"
}

proc mapgrab::stop {} {
    variable file
    variable screens

    if {$file eq ""} {
        return "not capturing"
    }

    close $file
    set file ""

    return "$screens screens written"
}
