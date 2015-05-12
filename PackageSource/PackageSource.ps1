
Set-StrictMode -Version 2 
$ErrorActionPreference = "Stop"

# User controlled parameters
$include_datetime_in_zipname = $true
#$remove_staging_folder_when_finished = $true
$remove_staging_folder_when_finished = $true


#Script parameters
$mydocs = [Environment]::GetFolderPath("MyDocuments")
$staging_prefix = "tmp_"
$zipfile_location = $mydocs 
$datestring = Get-Date -format yyyy-MM-dd

$scriptpath = $MyInvocation.MyCommand.Path
$script_dir = Split-Path $scriptpath

function RemoveSCCElementsAttributes($el)
{
    if ($el.Name.LocalName.StartsWith("Scc"))
    {
        # the the current element starts with Scc
        # Prune it and its children from the DOM
        $el.Remove();
        return;
    }
    else
    {
        # The current elemenent does not start with Scc
        # delete and Scc attributes it may have
        foreach ($attr in $el.Attributes())
        {
            if ($attr.Name.LocalName.StartsWith("Scc"))
            {
                $attr.Remove();
            }
        }

        # Check the children for any SCC Elements or attributes
        foreach ($child in $el.Elements())
        {
            RemoveSCCElementsAttributes($child);
        }
    }
}



# Calculate the root of the path we want to zip up
$src_root_path = resolve-path (join-path $script_dir "../..")

Write-Host From: $src_root_path 
Write-Host To: $zipfile_location

# Calculate the base name of the ZIP file
# By default just use the name of the directory that is being zipped
$basename = split-path -leaf $src_root_path 
if ($include_datetime_in_zipname)
{
    $basename = $basename + "-(" + $datestring  + ")"
}

$staging_folder = join-path $mydocs ($staging_prefix  + $basename) 

Write-host temp fldr: $staging_folder

# If staging folder exists already, delete it and anything inside of it
if ( test-path $staging_folder )
{
	Remove-Item $staging_folder -Recurse
}

# Create the staging folder
New-Item $staging_folder -ItemType directory

Write-host bin debug $src_root_path

# ---------------------------------
# COPY FILES TO THE STAGING FOLDER
# Remove the read-only flag with /A-:R
# Exclude Files with /XF option
#  *.suo 
#  *.user 
#  *.vssscc 
#  *.vspscc 
# Exclude folders with /XD option
#  bin
#  obj
#  _Resharper

# Control verbosity 
#  Don't show the names of files /NFL
#  Don't show the names of directories /NDL
&robocopy $src_root_path $staging_folder /MIR /A-:R /XF *.suo /XF *.user /XF *.vssscc /XF *.vspscc /NFL /NDL /XD bin /XD obj /XD _ReSharper*


# ---------------------------------
# UNBIND SLN FILES FROM SOURCE CONTROL
Write-Host Unbinding SLN files from Source Control
$slnfiles = Get-ChildItem $staging_folder *.sln -Recurse
foreach ($slnfile in $slnfiles) 
{
	$insection = $false
	write-host $slnfile
	$input_lines = get-content $slnfile.FullName
	$output_lines = new-object 'System.Collections.Generic.List[string]'

	foreach ($line in $input_lines) 
	{
		$line_trimmed = $line.Trim()

		if ($line_trimmed.StartsWith("GlobalSection(SourceCodeControl)") -Or $line_trimmed.StartsWith("GlobalSection(TeamFoundationVersionControl)"))
		{
			$insection = $true	
			# do not copy this line to output
		}
		elseif ($line_trimmed.StartsWith("EndGlobalSection"))
		{
			$insection = $false
			# do not copy this line to output
		}
		elseif ($line_trimmed.StartsWith("Scc"))
		{
			# do not copy this line to output
		}
		else
		{
			if ( !($insection))
			{
				$output_lines.Add( $line )
			}
		}

	}
	$output_lines | Out-File $slnfile.FullName
}


# ---------------------------------
# UNBIND PROJ FILES FROM SOURCE CONTROL
Write-Host Unbinding PROJ files from Source Control
$projfiles = Get-ChildItem $staging_folder *.*proj -Recurse
[Reflection.Assembly]::LoadWithPartialName("System.Xml.Linq") | Out-Null
foreach ($projfile in $projfiles) 
{
	$doc = [System.Xml.Linq.XDocument]::Load( $projfile.FullName )
	RemoveSCCElementsAttributes($doc.Root);
	$doc.Save( $projfile.FullName )
}

# ---------------------------------
# CREATE THE ZIP FILE
# We need to CD into the staging folder temporarily so that the ZIP
# does not contain an unneeded directory name
Write-Host Creating ZIP 

# caculate the name of the ZIP file
$zipfile = join-path $zipfile_location ( $basename +".zip" )
# If the zipfile already exists, then delete it
if ( test-path $zipfile )
{
    del $zipfile
}

# Find the location of 7Zip and verify it exists
$7zexe = join-path $script_dir "7za.exe"
if ( !(test-path $7zexe ) )
{
    $msg = "Cannot find " + $7zexe
    Write-Error $msg
}

$olddir = Get-Location
cd $staging_folder
&$7zexe a -tzip $zipfile 
cd $olddir


#-------------------------------------------------
# CLEAN UP
if ($remove_staging_folder_when_finished) 
{
    if ( test-path $staging_folder )
    {
    	Remove-Item $staging_folder -Recurse
    }
}



