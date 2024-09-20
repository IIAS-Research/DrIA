import { forwardRef, Inject, Injectable } from '@nestjs/common';
import { CreateUserDto } from './dto/create-user.dto';
import { UpdateUserDto, UpdateUserPasswordDto } from './dto/update-user.dto';
import { User } from './entities/user.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { AuthenticationService } from 'src/authentication/authentication.service';

@Injectable()
export class UsersService {
  constructor(
    @InjectRepository(User)
    private readonly usersRepository: Repository<User>,
    @Inject(forwardRef(() => AuthenticationService))
    private readonly authenticationService: AuthenticationService,
  ) {}
  async create(createUserDto: CreateUserDto) {
    createUserDto.password = await this.authenticationService.encryptPassword(
      createUserDto.password,
    );
    console.log(createUserDto);
    return await this.usersRepository.save(
      this.usersRepository.create(createUserDto),
    );
  }

  async findAll() {
    const users = await this.usersRepository.find();
    return users.map((user) => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { password, ...rest } = user;
      return rest;
    });
  }

  async findOne(id: string) {
    return await this.usersRepository.findOneBy({ id });
  }

  async findOneByEmail(email: string) {
    return await this.usersRepository.findOneByOrFail({ email });
  }

  async update(id: User['id'], updateUserDto: UpdateUserDto) {
    // Handle hash password in case the password is present in the DTO
    if (updateUserDto.password) {
      updateUserDto.password = await this.authenticationService.encryptPassword(
        updateUserDto.password,
      );
    }
    await this.usersRepository.update({ id }, updateUserDto);
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const { password, ...user } = await this.usersRepository.findOneBy({ id });
    return user;
  }

  async updatePassword(updatePasswordDto: UpdateUserPasswordDto) {
    return await this.usersRepository.update(
      { id: updatePasswordDto.userId },
      {
        password: await this.authenticationService.encryptPassword(
          updatePasswordDto.password,
        ),
      },
    );
  }

  async remove(id: string) {
    const userToRemove = await this.usersRepository.findOneByOrFail({ id });
    return await this.usersRepository.remove(userToRemove);
  }
}
