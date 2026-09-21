using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using HotelBooking.Core;
using HotelBooking.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HotelBooking.UnitTests
{
    public class RoomsControllerTests
    {
        private RoomsController controller;
        private IRepository<Room> fakeRoomRepository;

        public RoomsControllerTests()
        {
            var rooms = new List<Room>
            {
                new Room { Id=1, Description="A" },
                new Room { Id=2, Description="B" },
            };

            // Create fake RoomRepository.
            fakeRoomRepository = A.Fake<IRepository<Room>>();

            // Implement fake GetAll() method.
            A.CallTo(() => fakeRoomRepository.GetAllAsync()).Returns(rooms);


            // Implement fake Get() method.
            //A.CallTo(() => fakeRoomRepository.GetAsync(2)).Returns(rooms[1]);


            // Alternative setup with argument matchers:

            // Any integer:
            //A.CallTo(() => fakeRoomRepository.GetAsync(A<int>._)).Returns(rooms[1]);

            // Integers from 1 to 2 (using a predicate)
            // If the fake Get is called with an another argument value than 1 or 2,
            // it returns null, which corresponds to the behavior of the real
            // repository's Get method.
            //A.CallTo(() => fakeRoomRepository.GetAsync(A<int>.That.Matches(id => id > 0 && id < 3))).Returns(rooms[1]);

            // Integers from 1 to 2 (using a range)
            A.CallTo(() => fakeRoomRepository.GetAsync(
                A<int>.That.Matches(id => id >= 1 && id <= 2))).Returns(rooms[1]);


            // Create RoomsController
            controller = new RoomsController(fakeRoomRepository);
        }

        [Fact]
        public async Task GetAll_ReturnsListWithCorrectNumberOfRooms()
        {
            // Act
            var result = await controller.Get() as List<Room>;
            var noOfRooms = result.Count;

            // Assert
            Assert.Equal(2, noOfRooms);
        }

        [Fact]
        public async Task GetById_RoomExists_ReturnsIActionResultWithRoom()
        {
            // Act
            var result = await controller.Get(2) as ObjectResult;
            var room = result.Value as Room;
            var roomId = room.Id;

            // Assert
            Assert.InRange<int>(roomId, 1, 2);
        }

        [Fact]
        public async Task Delete_WhenIdIsLargerThanZero_RemoveIsCalled()
        {
            // Act
            await controller.Delete(1);

            // Assert against the fake object
            A.CallTo(() => fakeRoomRepository.RemoveAsync(1)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Delete_WhenIdIsLessThanOne_RemoveIsNotCalled()
        {
            // Act
            await controller.Delete(0);

            // Assert against the fake object
            A.CallTo(() => fakeRoomRepository.RemoveAsync(A<int>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Delete_WhenIdIsLargerThanTwo_RemoveThrowsException()
        {
            // Instruct the fake Remove method to throw an InvalidOperationException, if a room id that
            // does not exist in the repository is passed as a parameter. This behavior corresponds to
            // the behavior of the real repoository's Remove method.
            A.CallTo(() => fakeRoomRepository.RemoveAsync(
                    A<int>.That.Matches(id => id < 1 || id > 2)))
                    .Throws<InvalidOperationException>();

            Task result() => controller.Delete(3);
            
            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(result);

            // Assert against the fake object
            A.CallTo(() => fakeRoomRepository.RemoveAsync(A<int>._)).MustHaveHappened();
        }
    }
}
