import sys
import re 

#this.FindControl<MenuItem>("mnuSchuleLaden").Click += OnMnuSchoolLoadClick; 
#public void OnMnuExit(object? sender, RoutedEventArgs e)

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
            data = in_file.readlines()            
            with open(output_filename, 'w') as out_file:
                out_file.write("""// auto-generated with name2code.py\n""")
                for line in data:
                    type = re.search("[a-z]{2,4}",line).group()
                    out_file.write(pre_str+control[type]+middle_str+line.strip()+end_str+"On"+line.capitalize().strip()+"Click;\n")                
                for line in data:
                    type = re.search("[a-z]{2,4}",line).group()
                    out_file.write("public void On"+line.capitalize().strip()+"Click(object? sender, RoutedEventArgs e)\n{\n}")
    except IOError:
        sys.stderr.write(f"IO error. Make sure the input file exists and can be opened.\n")


if __name__ == "__main__":
    main()