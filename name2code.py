import sys
import re
from tokenize import group 
from collections import namedtuple

#this.FindControl<MenuItem>("mnuSchuleLaden").Click += OnMnuSchoolLoadClick; 
#public void OnMnuExit(object? sender, RoutedEventArgs e)

ControlItem = namedtuple('ControlItem',['name','type','shortType'])

def main():
    if len(sys.argv) != 3:
        sys.stderr.write(f"Usage: {sys.argv[0]} <input file> <output file>\n")
        exit(1)

    input_filename = sys.argv[1]
    output_filename = sys.argv[2]
    control = {"mnu":"MenuItem","tb":"TextBox","cb":"CheckBox","btn":"Button","cbox":"ComboBox"}
    pre_str = "this.FindControl<"
    middle_str=  ">(\""
    end_str = "\").Click += "
    try:
        with open(input_filename, 'r') as in_file:
            data = [value for value in in_file.readlines() if value.find("x:Name")>0]
            names=[]
            items=[]
            for line in data:
                name=re.search("x:Name=\"([a-zA-Z]*)\"",line)
                typ = re.search("<([a-zA-Z]*\ )",line)
                if (typ != None) and (name!=None):
                    names.append(name.groups()[0])
                    items.append(ControlItem(name.groups()[0],typ.groups()[0].rstrip(),re.search("[a-z]{2,4}",name.groups()[0]).group()))
            with open(output_filename, 'w') as out_file:
                out_file.write("""// auto-generated with name2code.py\n""")
                for item in items:
                    out_file.write("private "+getattr(item,'type') +" gui"+getattr(item,'name')+";\n")
                out_file.write("\n\n")
                for item in items:
                    out_file.write("gui"+getattr(item,'name') +" = this.FindControl<"+getattr(item,'type')+">(\""+getattr(item,'name')+"\");\n")
                out_file.write("\n\n")
                for line in names:
                    type = re.search("[a-z]{2,4}",line).group()
                    if type in control.keys():
                        out_file.write(pre_str+control[type]+middle_str+line.strip()+end_str+"On"+line.capitalize().strip()+"Click;\n")                
                out_file.write("\n\n")
                for line in names:
                    type = re.search("[a-z]{2,4}",line).group()
                    if type in control.keys():
                        out_file.write("public void On"+line.capitalize().strip()+"Click(object? sender, RoutedEventArgs e)\n{\n}")
    except IOError:
        sys.stderr.write(f"IO error. Make sure the input file exists and can be opened.\n")


if __name__ == "__main__":
    main()