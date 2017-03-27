# Script autoconfig logging feature 
# Use Router OS v5.24-v6.33.3-v6.4
# Date 17-Oct-2016 by AB

:global fname [system identity get name]

/system logging action
set 0 memory-lines=100
set 1 disk-file-count=30 disk-file-name=$fname disk-lines-per-file=500
:put "logging feature enabled"

/system logging
set 0 action=disk
set 1 action=disk
set 2 action=disk
set 3 action=disk
add disabled=yes topics=async
add topics=telephony
:put "system logging activated"

/system ntp client set enabled=yes primary-ntp=145.79.35.10
:put "NTP Server Changed"
	
:put "ALL DONE"