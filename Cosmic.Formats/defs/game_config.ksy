meta:
  id: game_config
  file-extension: bin
  imports:
    - pascal_string
    - stage_def
    - variable_def
seq:
  - id: game_name
    type: pascal_string
  - id: data_folder_name
    type: pascal_string
  - id: game_description
    type: pascal_string
    
  - id: num_script_types
    type: u1
  - id: script_types
    type: pascal_string
    repeat: expr
    repeat-expr: num_script_types
  - id: script_paths
    type: pascal_string
    repeat: expr
    repeat-expr: num_script_types
  
  - id: num_global_vars
    type: u1
  - id: global_vars
    type: variable_def
    repeat: expr
    repeat-expr: num_global_vars
  
  - id: num_global_sfx_paths
    type: u1
  - id: global_sfx_paths
    type: pascal_string
    repeat: expr
    repeat-expr: num_global_sfx_paths
  
  - id: num_player_names
    type: u1
  - id: player_names
    type: pascal_string
    repeat: expr
    repeat-expr: num_player_names
  
  - id: num_stages_presentation
    type: u1
  - id: stages_presentation
    type: stage_def
    repeat: expr
    repeat-expr: num_stages_presentation
  
  - id: num_stages_regular
    type: u1
  - id: stages_regular
    type: stage_def
    repeat: expr
    repeat-expr: num_stages_regular
  
  - id: num_stages_special
    type: u1
  - id: stages_special
    type: stage_def
    repeat: expr
    repeat-expr: num_stages_special
  
  - id: num_stages_bonus
    type: u1
  - id: stages_bonus
    type: stage_def
    repeat: expr
    repeat-expr: num_stages_bonus
