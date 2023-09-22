meta:
  id: anim_file
  endian: le
  imports:
    - pascal_string
types:
  animation:
    meta:  
      id: animation
      endian: le
    seq:
      - id: name
        type: pascal_string
      - id: frame_count
        type: u1
      - id: speed
        type: u1
      - id: loop_index
        type: u1
      - id: rotation_style
        type: u1
      - id: frames
        type: anim_frame
        repeat: expr
        repeat-expr: frame_count
  anim_frame:
    meta:
      id: anim_frame
      endian: le
    seq:
      - id: sheet
        type: u1
      - id: hitbox_id
        type: u1
      - id: spritesheet_x_offset
        type: u1
      - id: spritesheet_y_offset
        type: u1
      - id: width
        type: u1
      - id: height
        type: u1
      - id: pivot_x
        type: s1
      - id: pivot_y
        type: s1
  hitbox:
    meta:
      id: hitbox
      endian: le
    seq:
      - id: left
        type: s1
      - id: top
        type: s1
      - id: right
        type: s1
      - id: bottom
        type: s1
seq:
  - id: sheet_count
    type: u1
  - id: sheet_names
    type: pascal_string
    repeat: expr
    repeat-expr: sheet_count
  - id: anim_count
    type: u1
  - id: anims
    type: animation
    repeat: expr
    repeat-expr: anim_count
  - id: hitbox_count
    type: u1
  - id: hitboxes
    type: hitbox
    repeat: expr
    repeat-expr: hitbox_count